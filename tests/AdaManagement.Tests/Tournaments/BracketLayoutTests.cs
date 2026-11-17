using AdaManagement.Shared.Tournaments;
using FluentAssertions;

namespace AdaManagement.Tests.Tournaments;

/// <summary>A versenyág rajzának tiszta számításai (kártyák helye, továbblépés, könyökvonal).</summary>
public class BracketLayoutTests
{
    private const double H = 64, Gap = 14;

    private static BracketMatchDto M(int round, int index, BracketSegment segment = BracketSegment.Winner) => new(
        Guid.NewGuid(), segment, round, index, null, null, MatchStatus.Pending, null,
        MatchFormat.Bo3, ScoreType.Games, [], 0, 0, null, false);

    private static BracketRoundDto R(int round, params int[] indexes) => new(round, $"{round}. forduló", indexes.Select(i => M(round, i)).ToList());

    [Theory]
    [InlineData(4, 2, 0, 0)]
    [InlineData(4, 2, 1, 0)]
    [InlineData(4, 2, 3, 1)]
    [InlineData(2, 2, 1, 1)] // vesztes ág bekapcsolódó fordulója: 1:1
    [InlineData(3, 2, 2, 1)] // szabálytalan szélesség: arányosan
    [InlineData(4, 0, 3, 0)]
    public void TargetIndex_halves_maps_one_to_one_or_falls_back_to_proportional(int from, int to, int slot, int expected) =>
        BracketLayout.TargetIndex(from, to, slot).Should().Be(expected);

    [Fact]
    public void A_full_bracket_centres_every_match_between_its_two_feeders()
    {
        var layout = BracketLayout.ComputeTree([R(1, 0, 1, 2, 3), R(2, 0, 1), R(3, 0)], true, H, Gap);

        layout.Widths.Should().Equal(4, 2, 1);
        var row = H + Gap;
        layout.Positions[0].Values.Order().Should().Equal(H / 2, H / 2 + row, H / 2 + 2 * row, H / 2 + 3 * row);
        layout.Positions[1][0].Should().Be((layout.Positions[0][0] + layout.Positions[0][1]) / 2);
        layout.Positions[2][0].Should().Be((layout.Positions[1][0] + layout.Positions[1][1]) / 2);
        layout.TotalHeight.Should().Be(4 * row - Gap);
    }

    [Fact]
    public void Byes_leave_no_empty_rows_and_a_lone_feeder_stays_level_with_its_target()
    {
        // 5 fő egy 8-as ágban: az 1. fordulóban csak a 3-as helyen van meccs, a többi erőnyerő.
        var layout = BracketLayout.ComputeTree([R(1, 3), R(2, 0, 1), R(3, 0)], true, H, Gap);

        layout.Widths.Should().Equal(4, 2, 1);
        // Három levél: a 2. forduló 0-s meccse, az 1. forduló 3-as meccse — csak ezek foglalnak sort.
        layout.TotalHeight.Should().Be(2 * (H + Gap) - Gap);
        layout.Positions[1][1].Should().Be(layout.Positions[0][3]);
        layout.Positions[1][0].Should().BeLessThan(layout.Positions[1][1]);
    }

    [Fact]
    public void A_loser_bracket_takes_widths_from_its_own_rounds()
    {
        var widths = BracketLayout.RoundWidths([R(1, 0, 1), R(2, 0, 1), R(3, 0)], roundsAlwaysHalve: false);

        widths.Should().Equal(new Dictionary<int, int> { [1] = 2, [2] = 2, [3] = 1 });
    }

    [Fact]
    public void ElbowPath_uses_invariant_numbers()
    {
        BracketLayout.ElbowPath(0, 10.5, 32, 40).Should().Be("M0 10.5 H16 V40 H32");
    }

    private static BracketRoundDto L(int round, params int[] indexes) =>
        new(round, $"{round}. forduló", indexes.Select(i => M(round, i, BracketSegment.Loser)).ToList());

    [Theory]
    [InlineData(1, 1)]
    [InlineData(2, 2)]
    [InlineData(3, 4)]
    [InlineData(4, 6)]
    public void A_winner_round_sits_in_the_column_its_losers_drop_into(int round, int column) =>
        BracketLayout.WinnerColumn(round).Should().Be(column);

    [Fact]
    public void An_eight_player_double_elimination_lines_up_both_finals_and_centres_the_grand_final()
    {
        // 8 fő: győztes ág 4-2-1, vesztes ág 2-2-1-1, végül a nagydöntő.
        var winner = new[] { R(1, 0, 1, 2, 3), R(2, 0, 1), R(3, 0) };
        var loser = new[] { L(1, 0, 1), L(2, 0, 1), L(3, 0), L(4, 0) };
        var gf = M(1, 0, BracketSegment.GrandFinal);
        const double W = 184, C = 32, Label = 28, Section = 32;

        var grid = BracketLayout.ComputePlayoffGrid(winner, loser, gf, W, H, Gap, C, Label, Section);

        var col = W + C;
        var wFinal = grid.Cards.Single(c => c.Match == winner[2].Matches[0]);
        var lFinal = grid.Cards.Single(c => c.Match == loser[3].Matches[0]);
        var gfCard = grid.Cards.Single(c => c.Match == gf);
        wFinal.X.Should().Be(3 * col, "a 3. győztes forduló a 4. oszlopba kerül");
        lFinal.X.Should().Be(wFinal.X);
        lFinal.Y.Should().BeGreaterThan(wFinal.Y);
        gfCard.X.Should().Be(4 * col);
        gfCard.Y.Should().Be((wFinal.Y + lFinal.Y) / 2);
        grid.Width.Should().Be(4 * col + W);
        grid.Cards.Should().HaveCount(14);
        grid.Headers.Should().Contain(h => h.Title == "Nagydöntő");
        // Győztes ágon 4+2, vesztes ágon 2+2+1 vonal a következő fordulóba, plusz a két döntőből a nagydöntőbe.
        grid.Paths.Should().HaveCount(6 + 5 + 2);
    }
}
