param([Parameter(Mandatory=$true)][string]$Executable)
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing
$assembly = [Reflection.Assembly]::LoadFrom([IO.Path]::GetFullPath($Executable))
$routerType = $assembly.GetType('ScumMiniMap.RoadRouter', $true)
$router = $routerType.GetProperty('Instance').GetValue($null, $null)
$routerType.GetMethod('InitializeFromResource').Invoke($router, @()) | Out-Null
if (-not $routerType.GetProperty('IsLoaded').GetValue($router, $null)) {
    throw 'Packaged router could not verify and load its installation road asset.'
}
$find = $routerType.GetMethod('FindRoute')
foreach ($start in @(@(.40960548,.39800242),@(.50006073,.40556735),@(.57332342,.39900682))) {
    foreach ($destination in @(@(.10288965,.89523792),@(.30588488,.90629353),@(.50756363,.91399520),@(.69947680,.90420452),@(.89309654,.90364377))) {
        $from = [Drawing.PointF]::new($start[0],$start[1])
        $to = [Drawing.PointF]::new($destination[0],$destination[1])
        $route = $find.Invoke($router, @($from,$to))
        if (-not $route.Success -or $route.EntryDistanceMeters -gt 3 -or $route.ExitDistanceMeters -gt 3 -or $route.TotalDistanceMeters -gt 25000) {
            throw "Packaged B2-to-Z road route failed or detoured: $from -> $to"
        }
        Write-Output "PASS: Actual WPF package reaches Z-island road $to from northern B2 $from"
    }
}
foreach ($trip in @(
    @(0.62767,0.63618,0.67270,0.03878),
    @(0.35,0.45,0.295,0.835),
    @(0.63794,0.76210,0.62767,0.63618),
    @(0.50006073,0.40556735,0.10288965,0.89523792),
    @(0.50006073,0.40556735,0.30588488,0.90629353)
)) {
    $from = [Drawing.PointF]::new($trip[0],$trip[1])
    $to = [Drawing.PointF]::new($trip[2],$trip[3])
    $route = $find.Invoke($router, @($from,$to))
    if (-not $route.Success -or $route.Polyline.Length -lt 2) { throw "Packaged route failed: $from -> $to" }
    Write-Output "PASS: Actual WPF package routes $from -> $to ($([int]$route.TotalDistanceMeters) m)"
}
$lake = $find.Invoke($router, @([Drawing.PointF]::new(.418,.487),[Drawing.PointF]::new(.4181,.487)))
if ($lake.Success) { throw 'Packaged router accepted a lake shortcut.' }
Write-Output 'PASS: Actual WPF package rejects the lake shortcut.'
