$ErrorActionPreference = 'Stop'

Write-Host "Compiling standalone test harness for RoadRouter..."

$compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
$miniRoot = Split-Path $PSScriptRoot -Parent
$testBin = Join-Path $PSScriptRoot 'Test-RoadRouter.exe'
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
        double loadMs = (DateTime.UtcNow - t0).TotalMilliseconds;
        Console.WriteLine(string.Format("Loaded in {0:F1} ms. IsLoaded: {1}", loadMs, RoadRouter.Instance.IsLoaded));
        if (!RoadRouter.Instance.IsLoaded) {
            Console.WriteLine("FAILED: RoadRouter is not loaded.");
            return 1;
        }

        // Test 1: A1 Fish Factory to D1 Gorica
        PointF pA1 = new PointF(0.62767f, 0.63618f);
        PointF pD1 = new PointF(0.67270f, 0.03878f);
        Console.WriteLine("Planning route: A1 Fish Factory -> D1 Gorica...");
        DateTime t1 = DateTime.UtcNow;
        RoadRoute r1 = RoadRouter.Instance.FindRoute(pA1, pD1);
        double routeMs = (DateTime.UtcNow - t1).TotalMilliseconds;
        Console.WriteLine(string.Format("Route calculated in {0:F2} ms! Success: {1}", routeMs, r1.Success));
        if (!r1.Success || r1.Polyline == null || r1.Polyline.Length == 0) {
            Console.WriteLine("FAILED: Route was not found.");
            return 1;
        }
        Console.WriteLine(string.Format("Polyline Points: {0}, Total Distance: {1:F1} m ({2:F2} km)", r1.Polyline.Length, r1.TotalDistanceMeters, r1.TotalDistanceMeters / 1000.0));
        Console.WriteLine(string.Format("  Feeder Entry: {0:F1} m, Road: {1:F1} m, Feeder Exit: {2:F1} m", r1.EntryDistanceMeters, r1.RoadDistanceMeters, r1.ExitDistanceMeters));

        // Test 2: B1 Prkno to C3 Marof
        PointF pB1 = new PointF(0.67542f, 0.47654f);
        PointF pC3 = new PointF(0.27907f, 0.30848f);
        Console.WriteLine("Planning route: B1 Prkno -> C3 Marof...");
        DateTime t2 = DateTime.UtcNow;
        RoadRoute r2 = RoadRouter.Instance.FindRoute(pB1, pC3);
        double route2Ms = (DateTime.UtcNow - t2).TotalMilliseconds;
        Console.WriteLine(string.Format("Route calculated in {0:F2} ms! Success: {1}", route2Ms, r2.Success));
        if (!r2.Success) {
            Console.WriteLine("FAILED: Route B1 -> C3 was not found.");
            return 1;
        }
        Console.WriteLine(string.Format("Polyline Points: {0}, Total Distance: {1:F1} m ({2:F2} km)", r2.Polyline.Length, r2.TotalDistanceMeters, r2.TotalDistanceMeters / 1000.0));

        Console.WriteLine("ALL ROAD ROUTING UNIT TESTS PASSED!");
        return 0;
    }
}
"@

$harnessPath = Join-Path $PSScriptRoot 'TestHarness.cs'
[System.IO.File]::WriteAllText($harnessPath, $harnessCs, [System.Text.Encoding]::UTF8)

try {
    & $compiler /nologo /optimize+ /target:exe "/out:$testBin" /reference:System.Drawing.dll /reference:System.dll "/resource:$roadsBin,roads.bin" $routerCs $harnessPath
    if ($LASTEXITCODE -ne 0) { throw "Compilation failed" }

    Write-Host "Running Test-RoadRouter.exe..."
    & $testBin
    if ($LASTEXITCODE -ne 0) { throw "RoadRouter unit tests failed with code $LASTEXITCODE" }
} finally {
    if (Test-Path -LiteralPath $harnessPath) { Remove-Item -LiteralPath $harnessPath -Force }
    if (Test-Path -LiteralPath $testBin) { Remove-Item -LiteralPath $testBin -Force }
}
