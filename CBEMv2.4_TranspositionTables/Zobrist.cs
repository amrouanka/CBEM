using System;

public static class Zobrist
{
    // Random piece keys [piece][square]
    public static readonly ulong[,] pieceKeys = new ulong[12, 64];

    // Random enpassant keys [square]
    public static readonly ulong[] enpassantKeys = new ulong[64];

    // Random castling keys
    public static readonly ulong[] castleKeys = new ulong[16];

    // Random side key
    public static ulong sideKey;

    // Random number generator state
    private static uint randomState = 1804289383;

    // Hash key for board position (like unique position ID)
    public static ulong hashKey;

    static Zobrist()
    {
        /* In C#, a static constructor is guaranteed to be executed by the .NET runtime before any static members of the class are used  
        for the first time. So, the moment the search algorithm calls Evaluation.Evaluate(), the runtime ensures that
        InitializeTables() has already been run exactly once. */

        InitRandomKeys();    // Initialize Zobrist hash keys
    }

    // Get random 64-bit number using XOR-shift algorithm
    private static ulong GetRandomU64Number()
    {
        // XOR-shift random number generator
        randomState ^= randomState << 13;
        randomState ^= randomState >> 17;
        randomState ^= randomState << 5;

        // Combine multiple states to create 64-bit number
        ulong number1 = (ulong)randomState;
        randomState ^= randomState << 13;
        randomState ^= randomState >> 17;
        randomState ^= randomState << 5;
        ulong number2 = (ulong)randomState;

        return (number1 << 32) | number2;
    }

    // Initialize random hash keys
    public static void InitRandomKeys()
    {
        // Update pseudo random number state (its already defined but just for the sake of precaution)
        randomState = 1804289383;

        // Loop over piece codes (P=0 to k=11)
        for (int piece = Board.P; piece <= Board.k; piece++)
        {
            // Loop over board squares
            for (int square = 0; square < 64; square++)
            {
                // Init random piece keys
                pieceKeys[piece, square] = GetRandomU64Number();
            }
        }

        // Loop over board squares
        for (int square = 0; square < 64; square++)
        {
            // Init random enpassant keys
            enpassantKeys[square] = GetRandomU64Number();
        }

        // Loop over castling keys
        for (int index = 0; index < 16; index++)
        {
            // Init castling keys
            castleKeys[index] = GetRandomU64Number();
        }

        // Init random side key
        sideKey = GetRandomU64Number();
    }

    // Generate hash key for current board position
    public static ulong GenerateHashKey()
    {
        ulong hashKey = 0UL;

        // Hash pieces
        for (int piece = Board.P; piece <= Board.k; piece++)
        {
            ulong bitboard = Board.bitboards[piece];

            while (bitboard != 0)
            {
                int square = BitboardOperations.GetLs1bIndex(bitboard);
                hashKey ^= pieceKeys[piece, square];
                BitboardOperations.PopBit(ref bitboard, square);
            }
        }

        // Hash side to move
        if (Board.side == (int)Side.white)
            hashKey ^= sideKey;

        // Hash en passant square
        if (Board.enPassant != (int)Square.noSquare)
            hashKey ^= enpassantKeys[Board.enPassant];

        // Hash castling rights
        hashKey ^= castleKeys[Board.castle];

        return hashKey;
    }
}
