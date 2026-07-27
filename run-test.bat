@echo off
cd /d "C:\Program Files (x86)\Cute Chess"

echo --- Starting Test (Appending to existing results.pgn) ---
echo.

REM cmd="C:\Users\Rania\OneDrive\Desktop\CBEM\v2.5-tuned\bin\Release\net10.0\v2.5-tuned.exe" name=2.5-tuned ^
REM cmd="C:\Users\Rania\OneDrive\Desktop\CBEM\v2.6.2-search-micro-opts\bin\Release\net10.0\v2.6.2-search-micro-opts.exe" name=2.6.2-search-micro-opts ^
REM cmd="C:\Users\Rania\OneDrive\Desktop\CBEM\v2.6.3-search-flat-tables\bin\Release\net10.0\v2.6.3-search-flat-tables.exe" name=2.6.3-search-flat-tables ^
REM cmd="C:\Users\Rania\OneDrive\Desktop\CBEM\test-version\bin\Release\net10.0\test-version.exe" name=test-version ^
REM cmd="C:\Users\Rania\OneDrive\Desktop\CBEM\v2.6.4-search-mdp\bin\Release\net10.0\v2.6.4-search-mdp.exe" name=2.6.4-search-mdp ^

cutechess-cli.exe ^
  -engine cmd="C:\Users\Rania\OneDrive\Desktop\CBEM\v2.6.4-search-mdp\bin\Release\net10.0\v2.6.4-search-mdp.exe" name=2.6.4-search-mdp ^
  -engine cmd="C:\Users\Rania\OneDrive\Desktop\CBEM\v2.5-tuned\bin\Release\net10.0\v2.5-tuned.exe" name=2.5-tuned ^
  -each proto=uci tc=5+0.1 ^
  -games 2 ^
  -rounds 5000 ^
  -repeat ^
  -maxmoves 200 ^
  -openings file="C:\Users\Rania\Documents\8moves_v3.pgn" format=pgn order=sequential ^
  -concurrency 4 ^
  -pgnout "C:\Users\Rania\OneDrive\Desktop\results.pgn" ^
  -draw movenumber=40 movecount=8 score=12 ^
  -resign movecount=3 score=350

pause