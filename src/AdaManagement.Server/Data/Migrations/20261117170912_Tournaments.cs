using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AdaManagement.Server.Data.Migrations
{
    /// <inheritdoc />
    public partial class Tournaments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "tournament");

            migrationBuilder.CreateTable(
                name: "tournaments",
                schema: "tournament",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Date = table.Column<DateOnly>(type: "date", nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    Type = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    DefaultMatchFormat = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    DefaultScoreType = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    ThirdPlaceEnabled = table.Column<bool>(type: "bit", nullable: false),
                    GroupCount = table.Column<int>(type: "int", nullable: false),
                    GroupStageKind = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    PlayoffKind = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    PlayoffSize = table.Column<int>(type: "int", nullable: false),
                    SwissRounds = table.Column<int>(type: "int", nullable: false),
                    TiebreakerPolicy = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    GroupStageMatchFormat = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    UpperBracketFormat = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    LowerBracketFormat = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    GrandFinalFormat = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    PublicToken = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    Version = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tournaments", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "unmatched_scoreboard",
                schema: "tournament",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FiukWins = table.Column<int>(type: "int", nullable: false),
                    LanyokWins = table.Column<int>(type: "int", nullable: false),
                    LastVictor = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_unmatched_scoreboard", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "matches",
                schema: "tournament",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TournamentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Segment = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Round = table.Column<int>(type: "int", nullable: false),
                    IndexInRound = table.Column<int>(type: "int", nullable: false),
                    GroupIndex = table.Column<int>(type: "int", nullable: true),
                    ParticipantAId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ParticipantBId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    MatchFormat = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    WinnerId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ScoreType = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    CompletedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    CompletionSequence = table.Column<long>(type: "bigint", nullable: true),
                    WinnerToMatchId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    WinnerToSlotA = table.Column<bool>(type: "bit", nullable: true),
                    LoserToMatchId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    LoserToSlotA = table.Column<bool>(type: "bit", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_matches", x => x.Id);
                    table.ForeignKey(
                        name: "FK_matches_tournaments_TournamentId",
                        column: x => x.TournamentId,
                        principalSchema: "tournament",
                        principalTable: "tournaments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "participants",
                schema: "tournament",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TournamentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    Emoji = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: true),
                    Position = table.Column<int>(type: "int", nullable: false),
                    Seed = table.Column<int>(type: "int", nullable: false),
                    HasBye = table.Column<bool>(type: "bit", nullable: false),
                    GroupIndex = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_participants", x => x.Id);
                    table.ForeignKey(
                        name: "FK_participants_tournaments_TournamentId",
                        column: x => x.TournamentId,
                        principalSchema: "tournament",
                        principalTable: "tournaments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "unmatched_picks",
                schema: "tournament",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PlayerName = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    Character = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: false),
                    ScoreboardId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_unmatched_picks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_unmatched_picks_unmatched_scoreboard_ScoreboardId",
                        column: x => x.ScoreboardId,
                        principalSchema: "tournament",
                        principalTable: "unmatched_scoreboard",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "score_entries",
                schema: "tournament",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    MatchId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SequenceNumber = table.Column<int>(type: "int", nullable: false),
                    ScoreA = table.Column<int>(type: "int", nullable: true),
                    ScoreB = table.Column<int>(type: "int", nullable: true),
                    ParticipantAWon = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_score_entries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_score_entries_matches_MatchId",
                        column: x => x.MatchId,
                        principalSchema: "tournament",
                        principalTable: "matches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_matches_TournamentId",
                schema: "tournament",
                table: "matches",
                column: "TournamentId");

            migrationBuilder.CreateIndex(
                name: "IX_participants_TournamentId_Name",
                schema: "tournament",
                table: "participants",
                columns: new[] { "TournamentId", "Name" });

            migrationBuilder.CreateIndex(
                name: "IX_score_entries_MatchId_SequenceNumber",
                schema: "tournament",
                table: "score_entries",
                columns: new[] { "MatchId", "SequenceNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_tournaments_PublicToken",
                schema: "tournament",
                table: "tournaments",
                column: "PublicToken",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_unmatched_picks_ScoreboardId",
                schema: "tournament",
                table: "unmatched_picks",
                column: "ScoreboardId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "participants",
                schema: "tournament");

            migrationBuilder.DropTable(
                name: "score_entries",
                schema: "tournament");

            migrationBuilder.DropTable(
                name: "unmatched_picks",
                schema: "tournament");

            migrationBuilder.DropTable(
                name: "matches",
                schema: "tournament");

            migrationBuilder.DropTable(
                name: "unmatched_scoreboard",
                schema: "tournament");

            migrationBuilder.DropTable(
                name: "tournaments",
                schema: "tournament");
        }
    }
}
