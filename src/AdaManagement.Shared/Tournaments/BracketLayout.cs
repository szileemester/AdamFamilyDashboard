namespace AdaManagement.Shared.Tournaments;

/// <summary>
/// A versenyág rajzolásának tiszta számításai (az Adaminator BracketView-jából): melyik meccs hova lép
/// tovább, és hol áll függőlegesen az egyes meccsek kártyája.
/// </summary>
public static class BracketLayout
{
    /// <summary>Egy forduló meccseinek kártyaközéppontjai (indexInRound → px), a fordulók szélessége és a teljes magasság.</summary>
    public sealed record TreeLayout(IReadOnlyList<IReadOnlyDictionary<int, double>> Positions, IReadOnlyList<int> Widths, double TotalHeight);

    /// <summary>
    /// A következő forduló melyik helyére lép a <paramref name="slot"/> győztese. Feleződő fordulónál 2j és 2j+1
    /// a j-be, azonos szélességnél (vesztes ág bekapcsolódó fordulója) 1:1; minden más arányosan.
    /// </summary>
    public static int TargetIndex(int fromWidth, int toWidth, int slot)
    {
        if (toWidth <= 0) return 0;
        if (fromWidth == toWidth * 2) return slot / 2;
        if (fromWidth == toWidth) return slot;
        return Math.Min(toWidth - 1, slot * toWidth / fromWidth);
    }

    /// <summary>A forduló legnagyobb ténylegesen foglalt helye + 1 (a meccslista hézagos lehet: erőnyerőnek nincs meccse).</summary>
    public static int OccupiedWidth(BracketRoundDto round) =>
        round.Matches.Count == 0 ? 0 : round.Matches.Max(m => m.IndexInRound) + 1;

    /// <summary>
    /// Fordulónkénti helyszám. Feleződő ágnál (egyenes kiesés, győztes ág) a döntő 1 helyéből visszafelé
    /// duplázva; a vesztes ágnál a fordulók saját foglaltságából, de sosem keskenyebben, mint amit táplálnak.
    /// </summary>
    public static Dictionary<int, int> RoundWidths(IReadOnlyList<BracketRoundDto> rounds, bool roundsAlwaysHalve)
    {
        var widths = new Dictionary<int, int>();
        if (rounds.Count == 0) return widths;

        var last = rounds[^1];
        widths[last.Round] = roundsAlwaysHalve ? 1 : Math.Max(1, OccupiedWidth(last));
        for (var i = rounds.Count - 2; i >= 0; i--)
        {
            var next = widths[rounds[i + 1].Round];
            widths[rounds[i].Round] = roundsAlwaysHalve
                ? next * (1 << (rounds[i + 1].Round - rounds[i].Round))
                : Math.Max(OccupiedWidth(rounds[i]), next);
        }
        return widths;
    }

    /// <summary>
    /// Minden meccs kártyájának függőleges közepe. Csak a valódi meccsek foglalnak sort: amelyiket semmi sem
    /// táplálja („levél”), az a következő sorba kerül ágbeli sorrendben; a többi a saját táplálói közé középre.
    /// Így egy nem kettőhatvány létszámnál sem marad üres, nyújtott ág.
    /// </summary>
    public static TreeLayout ComputeTree(IReadOnlyList<BracketRoundDto> rounds, bool roundsAlwaysHalve, double cardHeight, double gap)
    {
        var widthMap = RoundWidths(rounds, roundsAlwaysHalve);
        var widths = rounds.Select(r => widthMap.TryGetValue(r.Round, out var w) ? w : Math.Max(1, OccupiedWidth(r))).ToList();
        var realSlots = rounds.Select(r => r.Matches.Select(m => m.IndexInRound).Order().ToList()).ToList();

        // Az előző forduló mely valódi meccsei táplálják ennek a fordulónak az egyes helyeit.
        var feedersOf = rounds.Select(_ => new Dictionary<int, List<int>>()).ToList();
        for (var ri = 1; ri < rounds.Count; ri++)
        {
            foreach (var slot in realSlots[ri - 1])
            {
                var target = TargetIndex(widths[ri - 1], widths[ri], slot);
                if (!feedersOf[ri].TryGetValue(target, out var list)) feedersOf[ri][target] = list = [];
                list.Add(slot);
            }
        }

        // A levél helye a teljes ágban, első fordulós egységben, hogy a különböző fordulók levelei is ágsorrendben álljanak.
        var firstWidth = widths.Count > 0 ? widths[0] : 1;
        double SpanStart(int ri, int slot) => slot * ((double)firstWidth / (widths[ri] == 0 ? 1 : widths[ri]));

        var leaves = new List<(int Ri, int Slot)>();
        for (var ri = 0; ri < rounds.Count; ri++)
            foreach (var slot in realSlots[ri])
                if (ri == 0 || !feedersOf[ri].TryGetValue(slot, out var f) || f.Count == 0)
                    leaves.Add((ri, slot));
        leaves.Sort((a, b) =>
        {
            var c = SpanStart(a.Ri, a.Slot).CompareTo(SpanStart(b.Ri, b.Slot));
            return c != 0 ? c : a.Ri.CompareTo(b.Ri);
        });

        var positions = rounds.Select(_ => new Dictionary<int, double>()).ToList();
        for (var row = 0; row < leaves.Count; row++)
            positions[leaves[row].Ri][leaves[row].Slot] = row * (cardHeight + gap) + cardHeight / 2;

        // A fordulók növekvő sorrendben jönnek, így egy meccs táplálói már a helyükön vannak.
        for (var ri = 1; ri < rounds.Count; ri++)
        {
            foreach (var slot in realSlots[ri])
            {
                if (positions[ri].ContainsKey(slot)) continue;
                var ys = (feedersOf[ri].TryGetValue(slot, out var f) ? f : [])
                    .Where(positions[ri - 1].ContainsKey)
                    .Select(s => positions[ri - 1][s])
                    .ToList();
                if (ys.Count > 0) positions[ri][slot] = ys.Average();
            }
        }

        var total = Math.Max(leaves.Count * (cardHeight + gap) - gap, cardHeight);
        return new TreeLayout(positions, widths, total);
    }

    /// <summary>A dupla kieséses rács egy kártyája (X a bal él, Y a függőleges közép), fejléce és a teljes rajz.</summary>
    public sealed record GridCard(BracketMatchDto Match, double X, double Y);

    public sealed record GridHeader(string Key, string Title, double X, double Top);

    public sealed record PlayoffGridLayout(double Width, double Height, IReadOnlyList<GridCard> Cards, IReadOnlyList<GridHeader> Headers, IReadOnlyList<string> Paths);

    /// <summary>
    /// A győztes ág fordulójának oszlopa a közös rácsban: az a vesztes forduló, ahová a vesztesei esnek
    /// (1–2. forduló egyenesen, utána 2r−2). Így a győztes ág döntője a vesztes ág döntőjével egy oszlopba kerül.
    /// A DoubleEliminationBracket.GenerateTopology bekapcsolódási rendjét követi: ha az változik, ezt is igazítani kell.
    /// </summary>
    public static int WinnerColumn(int round) => round <= 2 ? round : 2 * round - 2;

    /// <summary>
    /// A teljes dupla kieséses rájátszás egy rácson: felül a győztes ág, alatta a vesztes ág, jobbra a nagydöntő
    /// a két őt tápláló döntő között félúton. A kártyák abszolút helyűek, így a győztes ág oszlopot ugorhat.
    /// </summary>
    public static PlayoffGridLayout ComputePlayoffGrid(
        IReadOnlyList<BracketRoundDto> winnerRounds, IReadOnlyList<BracketRoundDto> loserRounds, BracketMatchDto? grandFinal,
        double cardWidth, double cardHeight, double gap, double connectorWidth, double labelHeight, double sectionGap)
    {
        var winnerLayout = ComputeTree(winnerRounds, true, cardHeight, gap);
        var loserLayout = ComputeTree(loserRounds, false, cardHeight, gap);

        var winnerTop = labelHeight;
        var winnerBottom = winnerTop + (winnerRounds.Count > 0 ? winnerLayout.TotalHeight : 0);
        var loserHeaderTop = winnerBottom + sectionGap;
        var loserTop = loserHeaderTop + labelHeight;
        var height = loserRounds.Count > 0 ? loserTop + loserLayout.TotalHeight : winnerBottom;

        double ColumnX(int column) => (column - 1) * (cardWidth + connectorWidth);
        var winnerColumns = winnerRounds.Select(r => WinnerColumn(r.Round)).ToList();
        var loserColumns = loserRounds.Select(r => r.Round).ToList();
        var lastColumn = winnerColumns.Concat(loserColumns).DefaultIfEmpty(1).Max();
        var grandFinalColumn = lastColumn + 1;
        var width = ColumnX(grandFinal is null ? lastColumn : grandFinalColumn) + cardWidth;

        var cards = new List<GridCard>();
        var headers = new List<GridHeader>();
        var paths = new List<string>();

        void Place(IReadOnlyList<BracketRoundDto> rounds, TreeLayout layout, List<int> columns, double top, double headerTop, string prefix)
        {
            for (var ri = 0; ri < rounds.Count; ri++)
            {
                var round = rounds[ri];
                var x = ColumnX(columns[ri]);
                headers.Add(new GridHeader($"{prefix}-{round.Round}", $"{prefix} · {round.Title}", x, headerTop));
                foreach (var match in round.Matches)
                {
                    if (!layout.Positions[ri].TryGetValue(match.IndexInRound, out var y)) continue;
                    cards.Add(new GridCard(match, x, top + y));
                    if (ri >= rounds.Count - 1) continue;
                    var target = TargetIndex(layout.Widths[ri], layout.Widths[ri + 1], match.IndexInRound);
                    if (layout.Positions[ri + 1].TryGetValue(target, out var ty))
                        paths.Add(ElbowPath(x + cardWidth, top + y, ColumnX(columns[ri + 1]), top + ty));
                }
            }
        }

        Place(winnerRounds, winnerLayout, winnerColumns, winnerTop, 0, "Győztes ág");
        Place(loserRounds, loserLayout, loserColumns, loserTop, loserHeaderTop, "Vesztes ág");

        if (grandFinal is not null)
        {
            (double X, double Y)? FinalOf(IReadOnlyList<BracketRoundDto> rounds, TreeLayout layout, double top, List<int> columns)
            {
                if (rounds.Count == 0 || rounds[^1].Matches.Count == 0) return null;
                var match = rounds[^1].Matches[0];
                return layout.Positions[^1].TryGetValue(match.IndexInRound, out var y) ? (ColumnX(columns[^1]), top + y) : null;
            }

            var winnerFinal = FinalOf(winnerRounds, winnerLayout, winnerTop, winnerColumns);
            var loserFinal = FinalOf(loserRounds, loserLayout, loserTop, loserColumns);
            var x = ColumnX(grandFinalColumn);
            var y = winnerFinal is { } w && loserFinal is { } l ? (w.Y + l.Y) / 2 : (winnerFinal ?? loserFinal)?.Y ?? labelHeight + cardHeight / 2;

            headers.Add(new GridHeader("grand-final", "Nagydöntő", x, 0));
            cards.Add(new GridCard(grandFinal, x, y));
            foreach (var source in new[] { winnerFinal, loserFinal })
                if (source is { } src) paths.Add(ElbowPath(src.X + cardWidth, src.Y, x, y));
        }

        return new PlayoffGridLayout(width, Math.Max(height, labelHeight + cardHeight), cards, headers, paths);
    }

    /// <summary>Könyökvonal két kártya között: vízszintesen félútig, függőlegesen, majd vízszintesen a célig.</summary>
    public static string ElbowPath(double fromX, double fromY, double toX, double toY)
    {
        var midX = (fromX + toX) / 2;
        return FormattableString.Invariant($"M{fromX} {fromY} H{midX} V{toY} H{toX}");
    }
}
