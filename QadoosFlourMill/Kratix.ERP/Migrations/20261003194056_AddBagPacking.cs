using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Kratix.ERP.Migrations
{
    /// <inheritdoc />
    public partial class AddBagPacking : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "KgPerBag",
                table: "Products",
                type: "decimal(15,3)",
                precision: 15,
                scale: 3,
                nullable: true);

            migrationBuilder.AlterColumn<decimal>(
                name: "ReceivedQuantity",
                table: "POLineItems",
                type: "decimal(15,3)",
                precision: 15,
                scale: 3,
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "decimal(18,2)");

            migrationBuilder.AlterColumn<decimal>(
                name: "Moisture",
                table: "InboundReceipts",
                type: "decimal(5,2)",
                precision: 5,
                scale: 2,
                nullable: true,
                oldClrType: typeof(decimal),
                oldType: "decimal(18,2)",
                oldNullable: true);

            migrationBuilder.AlterColumn<decimal>(
                name: "Impurities",
                table: "InboundReceipts",
                type: "decimal(5,2)",
                precision: 5,
                scale: 2,
                nullable: true,
                oldClrType: typeof(decimal),
                oldType: "decimal(18,2)",
                oldNullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "PackedQuantityKg",
                table: "BatchMaterials",
                type: "decimal(15,3)",
                precision: 15,
                scale: 3,
                nullable: false,
                defaultValue: 0m);

            // Do not infer units or subtract historical sales from arbitrary data.
            // Already-applied databases are untouched. Older populated databases
            // must reconcile bag units explicitly before applying this migration.
            migrationBuilder.Sql(@"
                IF EXISTS (SELECT 1 FROM Inventories i JOIN Products p ON p.ProductID = i.ProductID WHERE p.Unit = 'bag' AND i.QuantityAvailable <> 0)
                   OR EXISTS (SELECT 1 FROM BatchMaterials bm JOIN Products p ON p.ProductID = bm.ProductID JOIN ProductionBatches pb ON pb.BatchID = bm.BatchID
                              WHERE p.Unit = 'bag' AND bm.MaterialType = 'Output' AND pb.Status = 'Completed')
                    THROW 51001, 'Legacy bag stock needs explicit reconciliation. See DATABASE_UPGRADE.md; no stock was converted.', 1;
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                IF EXISTS (SELECT 1 FROM Inventories i JOIN Products p ON p.ProductID = i.ProductID WHERE p.Unit = 'bag' AND i.QuantityAvailable <> 0)
                   OR EXISTS (SELECT 1 FROM BatchMaterials WHERE PackedQuantityKg <> 0)
                    THROW 51002, 'Cannot undo bag packing with stock present. Restore a verified backup instead.', 1;
            ");
            migrationBuilder.DropColumn(
                name: "KgPerBag",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "PackedQuantityKg",
                table: "BatchMaterials");

            migrationBuilder.AlterColumn<decimal>(
                name: "ReceivedQuantity",
                table: "POLineItems",
                type: "decimal(18,2)",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "decimal(15,3)",
                oldPrecision: 15,
                oldScale: 3);

            migrationBuilder.AlterColumn<decimal>(
                name: "Moisture",
                table: "InboundReceipts",
                type: "decimal(18,2)",
                nullable: true,
                oldClrType: typeof(decimal),
                oldType: "decimal(5,2)",
                oldPrecision: 5,
                oldScale: 2,
                oldNullable: true);

            migrationBuilder.AlterColumn<decimal>(
                name: "Impurities",
                table: "InboundReceipts",
                type: "decimal(18,2)",
                nullable: true,
                oldClrType: typeof(decimal),
                oldType: "decimal(5,2)",
                oldPrecision: 5,
                oldScale: 2,
                oldNullable: true);
        }
    }
}
