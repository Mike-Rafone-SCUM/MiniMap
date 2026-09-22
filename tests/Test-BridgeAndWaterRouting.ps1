$ErrorActionPreference = 'Stop'

Write-Host "Compiling standalone bridge and water avoidance test harness..."

$compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
$miniRoot = Split-Path $PSScriptRoot -Parent
$testBin = Join-Path $PSScriptRoot 'Test-BridgeAndWaterRouter.exe'
$routerCs = Join-Path $miniRoot 'src\RoadRouter.cs'
$roadsBin = Join-Path $miniRoot 'resources\roads.bin'

$harnessCs = @"
using System;
using System.Drawing;
using System.IO;
using ScumMiniMap;

public class TestHarness {
    public static int Main(string[] args) {
        Console.WriteLine("Initializing RoadRouter...");
        DateTime t0 = DateTime.UtcNow;
        RoadRouter.Instance.InitializeFromResource();
        Console.WriteLine(string.Format("Loaded in {0:F1} ms. IsLoaded: {1}", (DateTime.UtcNow - t0).TotalMilliseconds, RoadRouter.Instance.IsLoaded));

        // Test 1: Diagonal Railway Bridge (Mainland rail -> Island rail)
        PointF pRailMain = new PointF(0.46625f, 0.74648f);
        PointF pRailIsland = new PointF(0.49277f, 0.77264f);
        Console.WriteLine("\n--- Test 1: Diagonal Railway Bridge crossing ---");
        RoadRoute rRail = RoadRouter.Instance.FindRoute(pRailMain, pRailIsland);
        Console.WriteLine(string.Format("Success: {0}, HasWaterTransit: {1}, Dist: {2:F1} m, Polyline pts: {3}",
            rRail.Success, rRail.HasWaterTransit, rRail.TotalDistanceMeters, rRail.Polyline != null ? rRail.Polyline.Length : 0));
        if (!rRail.Success) {
            Console.WriteLine("FAIL: Diagonal railway route failed.");
            return 1;
        }
        if (rRail.HasWaterTransit) {
            Console.WriteLine("FAIL: Diagonal railway bridge used multi-modal water transit instead of the bridge!");
            return 1;
        }
        if (rRail.TotalDistanceMeters > 1500) {
            Console.WriteLine("FAIL: Diagonal railway bridge route distance unexpectedly long: " + rRail.TotalDistanceMeters);
            return 1;
        }
        Console.WriteLine("PASS: Diagonal Railway Bridge connected seamlessly as land/rail!");

        // Test 2: Central-East Highway Bridge (A1 -> Z1)
        PointF pEastHwyMain = new PointF(0.64612f, 0.72455f);
        PointF pEastHwyIsland = new PointF(0.62606f, 0.79659f);
        Console.WriteLine("\n--- Test 2: Central-East Highway Bridge crossing ---");
        RoadRoute rHwy = RoadRouter.Instance.FindRoute(pEastHwyMain, pEastHwyIsland);
        Console.WriteLine(string.Format("Success: {0}, HasWaterTransit: {1}, Dist: {2:F1} m, Polyline pts: {3}",
            rHwy.Success, rHwy.HasWaterTransit, rHwy.TotalDistanceMeters, rHwy.Polyline != null ? rHwy.Polyline.Length : 0));
        if (!rHwy.Success || rHwy.HasWaterTransit) {
            Console.WriteLine("FAIL: Central-East highway bridge route failed or used water.");
            return 1;
        }
        Console.WriteLine("PASS: Central-East Highway Bridge routed successfully without water!");

        // Test 3: West Rogoznica Bridge (A3 -> Z3)
        // Use the mapped Rogoznica bridge approaches; the old .18,.81 point is in the sea.
        PointF pWestMain = new PointF(0.28624f, 0.75685f);
        PointF pWestIsland = new PointF(0.29397f, 0.82816f);
        Console.WriteLine("\n--- Test 3: West Rogoznica Bridge crossing ---");
        RoadRoute rWest = RoadRouter.Instance.FindRoute(pWestMain, pWestIsland);
        Console.WriteLine(string.Format("Success: {0}, HasWaterTransit: {1}, Dist: {2:F1} m",
            rWest.Success, rWest.HasWaterTransit, rWest.TotalDistanceMeters));
        if (!rWest.Success || rWest.HasWaterTransit) {
            Console.WriteLine("FAIL: West bridge route failed or used water.");
            return 1;
        }
        Console.WriteLine("PASS: West Rogoznica Bridge routed without water!");

        // Test 4: Long cross-island trip (Mainland interior -> South Island interior)
        PointF pMainlandInterior = new PointF(0.35000f, 0.45000f); // B3 / B2 area
        PointF pIslandInterior = new PointF(0.29500f, 0.83500f); // Z island road, dry, near Rogoznica bridge S
        Console.WriteLine("\n--- Test 4: Mainland Interior -> South Island Interior ---");
        RoadRoute rCross = RoadRouter.Instance.FindRoute(pMainlandInterior, pIslandInterior);
        Console.WriteLine(string.Format("Success: {0}, HasWaterTransit: {1}, Dist: {2:F1} m",
            rCross.Success, rCross.HasWaterTransit, rCross.TotalDistanceMeters));
        if (!rCross.Success || rCross.HasWaterTransit) {
            Console.WriteLine("FAIL: Mainland-to-Island route used water transit instead of bridge!");
            return 1;
        }
        Console.WriteLine("PASS: Mainland to South Island took bridges with ZERO water traversal!");

        // Test 5: Off-road Mainland to Mainland (water avoidance test)
        PointF pOffroad1 = new PointF(0.55000f, 0.35000f);
        PointF pOffroad2 = new PointF(0.60000f, 0.50000f);
        Console.WriteLine("\n--- Test 5: Off-road Mainland to Mainland ---");
        RoadRoute rOff = RoadRouter.Instance.FindRoute(pOffroad1, pOffroad2);
        Console.WriteLine(string.Format("Success: {0}, HasWaterTransit: {1}, Dist: {2:F1} m",
            rOff.Success, rOff.HasWaterTransit, rOff.TotalDistanceMeters));
        if (!rOff.Success || rOff.HasWaterTransit) {
            Console.WriteLine("FAIL: Mainland off-road route improperly used water transit!");
            return 1;
        }
        Console.WriteLine("PASS: Mainland off-road route stayed on land/road with ZERO water traversal!");

        var water=new RoutingWaterMask(); water.Load();
        if(!water.IsLoaded) { Console.WriteLine("FAIL: Water coverage not loaded."); return 1; }
        // Reproduction around the B2 town stream/lake: the old entry feeder crossed water.
        foreach(float x in new[]{.411f,.414f,.417f}) {
            PointF start=new PointF(x,.455f),target=new PointF(.439f,.463f);
            RoadRoute route=RoadRouter.Instance.FindRoute(start,target);
            if(!route.Success || route.HasWaterTransit || !water.AllowsConnector(start,route.RoadEntryPoint) || !water.AllowsConnector(route.RoadExitPoint,target)) {
                Console.WriteLine("FAIL: B2 shoreline route crossed mapped water or lost the dry alternative."); return 1;
            }
        }
        Console.WriteLine("PASS: B2 town routes use dry entry/exit connections.");
        PointF pOceanFar = new PointF(0.02000f, 0.50000f); // deep ocean, west edge
        RoadRoute rOcean = RoadRouter.Instance.FindRoute(pMainlandInterior, pOceanFar);
        if (rOcean.Success || rOcean.HasWaterTransit) { Console.WriteLine("FAIL: Invented offshore road route."); return 1; }
        RoadRoute formerSea=RoadRouter.Instance.FindRoute(pMainlandInterior,new PointF(.4f,.82f));
        if(formerSea.Success) { Console.WriteLine("FAIL: Old offshore test destination still accepted."); return 1; }
        PointF lake=new PointF(.418f,.487f);
        RoadRoute shortWater=RoadRouter.Instance.FindRoute(lake,new PointF(.4181f,.487f));
        if(water.AllowsConnector(lake,lake) || shortWater.Success) { Console.WriteLine("FAIL: Short-distance shortcut accepts water."); return 1; }
        if(new RoutingWaterMask().AllowsConnector(pMainlandInterior,pMainlandInterior)) { Console.WriteLine("FAIL: Missing mask permits unverified connector."); return 1; }
        Console.WriteLine("PASS: Offshore and short lake shortcuts rejected; missing coverage fails closed.");

        Console.WriteLine("\nALL BRIDGE AND WATER AVOIDANCE TESTS PASSED SUCCESSFULLY!");
        return 0;
    }
}
"@

$harnessPath = Join-Path $PSScriptRoot 'TestBridgeHarness.cs'
[System.IO.File]::WriteAllText($harnessPath, $harnessCs, [System.Text.Encoding]::UTF8)

try {
    & $compiler /nologo /optimize+ /target:exe "/out:$testBin" /reference:System.Drawing.dll /reference:System.dll "/resource:$roadsBin,roads.bin" $routerCs $harnessPath
    if ($LASTEXITCODE -ne 0) { throw "Compilation failed" }

    Write-Host "Running Test-BridgeAndWaterRouter.exe..."
    & $testBin
    if ($LASTEXITCODE -ne 0) { throw "Bridge unit tests failed with code $LASTEXITCODE" }
} finally {
    if (Test-Path -LiteralPath $harnessPath) { Remove-Item -LiteralPath $harnessPath -Force }
    if (Test-Path -LiteralPath $testBin) { Remove-Item -LiteralPath $testBin -Force }
}
