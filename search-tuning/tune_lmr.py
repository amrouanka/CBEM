import json
import random
import re
import subprocess
from dataclasses import dataclass
from pathlib import Path

# ---------------- CONFIG ----------------

CUTECHESS = r"C:\Program Files (x86)\Cute Chess\cutechess-cli.exe"
ENGINE_EXE = r"C:\Users\Rania\OneDrive\Desktop\CBEM\search-tuning\bin\Release\net10.0\search-tuning.exe"
OPENINGS = r"C:\Users\Rania\Documents\8moves_v3.pgn"

WORKDIR = Path(r"C:\cbem_tuning\lmr")
WORKDIR.mkdir(parents=True, exist_ok=True)

TC = "1+0.1"
CONCURRENCY = 4
ROUNDS_PER_ITER = 16
ITERATIONS = 100

MAXMOVES = 200
DRAW_MOVENUMBER = 40
DRAW_MOVECOUNT = 8
DRAW_SCORE = 12
RESIGN_MOVECOUNT = 3
RESIGN_SCORE = 350

A = 20.0
ALPHA = 0.602
GAMMA = 0.101
A0 = 0.20
C0 = 0.10

RANDOM_SEED = 111111
random.seed(RANDOM_SEED)

LOG_FILE = WORKDIR / "lmr_tuning_log.jsonl"
BEST_FILE = WORKDIR / "lmr_best_params.json"

# --------------- PARAMETERS ---------------

@dataclass
class Param:
    name: str
    arg: str
    default: int
    minv: int
    maxv: int

PARAMS = [
    Param("FullDepthMoves", "fdm", 4, 1, 8),
    Param("ReductionLimit", "rl", 3, 1, 6),
    Param("LmrBase", "lmrbase", 1, 0, 3),
    Param("LmrDivisor", "lmrdiv", 2, 1, 5),
]

# Fixed params (not tuned, but must be passed)
FIXED_PARAMS = {
    "aw": 50,
    "awmindepth": 4,
    "rfpdepth": 3,
    "rfp": 150,
    "fpdepth": 3,
    "fp": 120,
    "nmdepth": 3,
    "nmbase": 3,
    "nmdepthdiv": 4,
    "nmevaldiv": 200,
    "nmbonuscap": 3,
    "qsdelta": 200,
}

# --------------- UTILS ---------------

def clamp(x, lo, hi):
    return lo if x < lo else hi if x > hi else x

def norm_from_value(p: Param, v: int) -> float:
    if p.maxv == p.minv:
        return 0.0
    return (v - p.minv) / (p.maxv - p.minv)

def value_from_norm(p: Param, x: float) -> int:
    x = clamp(x, 0.0, 1.0)
    v = p.minv + x * (p.maxv - p.minv)
    return int(round(v))

def theta_to_values(theta):
    values = dict(FIXED_PARAMS)
    for i, p in enumerate(PARAMS):
        values[p.arg] = value_from_norm(p, theta[i])
    return values

def values_to_arg_string(values):
    return " ".join(f"--{k}={v}" for k, v in values.items())

def write_wrapper(path: Path, args_string: str):
    text = f'@echo off\n"{ENGINE_EXE}" {args_string}\n'
    path.write_text(text, encoding="utf-8")

def parse_score(output: str):
    m = re.search(r"Score of .*: (\d+) - (\d+) - (\d+)", output)
    if not m:
        raise RuntimeError("Could not parse cutechess score.\n\n" + output)
    wins = int(m.group(1))
    losses = int(m.group(2))
    draws = int(m.group(3))
    total = wins + losses + draws
    score = (wins - losses) / total
    return wins, losses, draws, score

def run_match(plus_values, minus_values, iteration):
    plus_cmd = WORKDIR / f"plus_{iteration}.cmd"
    minus_cmd = WORKDIR / f"minus_{iteration}.cmd"

    write_wrapper(plus_cmd, values_to_arg_string(plus_values))
    write_wrapper(minus_cmd, values_to_arg_string(minus_values))

    cmd = [
        CUTECHESS,
        "-engine", f"cmd={str(plus_cmd)}", "name=plus",
        "-engine", f"cmd={str(minus_cmd)}", "name=minus",
        "-each", "proto=uci", f"tc={TC}",
        "-games", "2",
        "-rounds", str(ROUNDS_PER_ITER),
        "-repeat",
        "-maxmoves", str(MAXMOVES),
        "-openings", f"file={OPENINGS}", "format=pgn", "order=sequential",
        "-concurrency", str(CONCURRENCY),
        "-draw", f"movenumber={DRAW_MOVENUMBER}", f"movecount={DRAW_MOVECOUNT}", f"score={DRAW_SCORE}",
        "-resign", f"movecount={RESIGN_MOVECOUNT}", f"score={RESIGN_SCORE}",
    ]

    print(f"\nIteration {iteration}")
    print("PLUS :", {p.arg: plus_values[p.arg] for p in PARAMS})
    print("MINUS:", {p.arg: minus_values[p.arg] for p in PARAMS})

    result = subprocess.run(
        cmd,
        capture_output=True,
        text=True,
        encoding="utf-8",
        errors="replace"
    )

    output = result.stdout + "\n" + result.stderr
    if result.returncode != 0:
        raise RuntimeError(output)

    wins, losses, draws, score = parse_score(output)
    print(f"Result: +{wins} -{losses} ={draws}  score={score:.4f}")
    return wins, losses, draws, score

def verify(baseline_values, tuned_values):
    print("\n=== FINAL VERIFICATION ===")
    wins, losses, draws, score = run_match(tuned_values, baseline_values, "final")
    print("\nTuned vs Baseline")
    print(f"+{wins} -{losses} ={draws} score={score:.4f}")

# --------------- SPSA ---------------

def main():
    theta = [norm_from_value(p, p.default) for p in PARAMS]
    baseline_values = theta_to_values(theta)

    print("=== LMR TUNING ===")
    print("Tuning:", [p.name for p in PARAMS])
    print("BASELINE:", {p.arg: baseline_values[p.arg] for p in PARAMS})

    for k in range(ITERATIONS):
        ak = A0 / ((k + 1 + A) ** ALPHA)
        ck = C0 / ((k + 1) ** GAMMA)

        delta = [1 if random.random() < 0.5 else -1 for _ in PARAMS]

        theta_plus = [clamp(theta[i] + ck * delta[i], 0.0, 1.0) for i in range(len(PARAMS))]
        theta_minus = [clamp(theta[i] - ck * delta[i], 0.0, 1.0) for i in range(len(PARAMS))]

        plus_values = theta_to_values(theta_plus)
        minus_values = theta_to_values(theta_minus)

        wins, losses, draws, score = run_match(plus_values, minus_values, k + 1)

        for i in range(len(PARAMS)):
            ghat = score / (2.0 * ck * delta[i])
            theta[i] = clamp(theta[i] + ak * ghat, 0.0, 1.0)

        current_values = theta_to_values(theta)

        record = {
            "iteration": k + 1,
            "wins": wins,
            "losses": losses,
            "draws": draws,
            "score": score,
            "current": {p.arg: current_values[p.arg] for p in PARAMS},
        }

        with LOG_FILE.open("a", encoding="utf-8") as f:
            f.write(json.dumps(record) + "\n")

        BEST_FILE.write_text(json.dumps({p.arg: current_values[p.arg] for p in PARAMS}, indent=2), encoding="utf-8")

        print("CURRENT:", {p.arg: current_values[p.arg] for p in PARAMS})

    tuned_values = theta_to_values(theta)

    print("\n=== LMR TUNING COMPLETE ===")
    print(json.dumps({p.arg: tuned_values[p.arg] for p in PARAMS}, indent=2))

    verify(baseline_values, tuned_values)

if __name__ == "__main__":
    main()