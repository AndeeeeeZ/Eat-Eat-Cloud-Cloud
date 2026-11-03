using System;
using System.Collections.Generic;

public static class MP_ResultsSceneData
{
    private static MP_RoundStanding[] topPlayers = Array.Empty<MP_RoundStanding>();

    public static string WinnerName { get; private set; } = "";
    public static IReadOnlyList<MP_RoundStanding> TopPlayers => topPlayers;
    public static bool HasResults { get; private set; }

    public static void Set(string winnerName, MP_RoundStanding[] players)
    {
        WinnerName = winnerName ?? "";
        topPlayers = players != null ? (MP_RoundStanding[])players.Clone() :
            Array.Empty<MP_RoundStanding>();
        HasResults = true;
    }

    public static void Clear()
    {
        WinnerName = "";
        topPlayers = Array.Empty<MP_RoundStanding>();
        HasResults = false;
    }
}
