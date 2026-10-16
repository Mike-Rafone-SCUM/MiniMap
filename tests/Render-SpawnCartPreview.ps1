param(
    [Parameter(Mandatory=$true)][string]$AssemblyPath,
    [Parameter(Mandatory=$true)][string]$OutputPath,
    [string]$ModifierOutputPath,
    [string]$KitOutputPath,
    [string]$Language = 'en'
)
$ErrorActionPreference = 'Stop'
if ([Threading.Thread]::CurrentThread.GetApartmentState() -ne 'STA') {
    throw 'Run this preview helper with powershell.exe -NoProfile -STA -File.'
}

# Render an existing canonical build in an off-screen window whose activation is vetoed.
# This lets native ListView group headers paint, without focus changes or game input.
$assemblyFile = (Resolve-Path -LiteralPath $AssemblyPath).ProviderPath
$previewOutput = [IO.Path]::GetFullPath($OutputPath)
$previewDirectory = [IO.Path]::GetDirectoryName($previewOutput)
if (-not (Test-Path -LiteralPath $previewDirectory)) {
    New-Item -ItemType Directory -Path $previewDirectory -Force | Out-Null
}
Add-Type -AssemblyName System.Drawing
Add-Type -AssemblyName System.Windows.Forms
Add-Type -ReferencedAssemblies WindowsBase,PresentationCore,PresentationFramework,System.Xaml,Accessibility,System.Windows.Forms,System.Drawing -TypeDefinition @'
using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;
public sealed class SpawnPreviewInactiveUi : IDisposable {
    delegate IntPtr ActivationHook(int code, IntPtr first, IntPtr second);
    [DllImport("user32.dll")] static extern IntPtr SetWindowsHookEx(int id, ActivationHook callback, IntPtr module, uint thread);
    [DllImport("user32.dll")] static extern bool UnhookWindowsHookEx(IntPtr hook);
    [DllImport("user32.dll")] static extern IntPtr CallNextHookEx(IntPtr hook, int code, IntPtr first, IntPtr second);
    [DllImport("kernel32.dll")] static extern uint GetCurrentThreadId();
    [DllImport("user32.dll", EntryPoint="GetWindowLongW")] static extern int WindowStyle(IntPtr window, int index);
    [DllImport("user32.dll", EntryPoint="SetWindowLongW")] static extern int SetWindowStyle(IntPtr window, int index, int value);
    readonly ActivationHook callback;
    readonly IntPtr hook;
    public SpawnPreviewInactiveUi() {
        callback=(code, first, second)=>code==5?new IntPtr(1):CallNextHookEx(IntPtr.Zero, code, first, second);
        hook=SetWindowsHookEx(5, callback, IntPtr.Zero, GetCurrentThreadId());
        if(hook==IntPtr.Zero)throw new Exception("Could not prevent preview-window activation.");
    }
    public void Show(Form dialog) {
        dialog.TopMost=false;dialog.ShowInTaskbar=false;dialog.StartPosition=FormStartPosition.Manual;
        dialog.Location=new Point(-32000,-32000);
        SetWindowStyle(dialog.Handle,-20,WindowStyle(dialog.Handle,-20)|0x08000000);
        dialog.Show();dialog.PerformLayout();Application.DoEvents();
    }
    public void Dispose() { UnhookWindowsHookEx(hook);GC.KeepAlive(callback); }
}
'@
[Windows.Forms.Application]::EnableVisualStyles()
$assembly = [Reflection.Assembly]::LoadFrom($assemblyFile)
$assembly.GetType('ScumMiniMap.Localization', $true).GetMethod('SetLanguage').Invoke($null,[object[]]@($Language)) | Out-Null
$catalogueType = $assembly.GetType('ScumMiniMap.SpawnItemCatalog', $true)
$dialogType = $assembly.GetType('ScumMiniMap.SpawnCartDialog', $true)
$instanceFlags = [Reflection.BindingFlags]'Instance,NonPublic'
$staticFlags = [Reflection.BindingFlags]'Static,NonPublic'
$loadCatalogue = $catalogueType.GetMethod('LoadBundled', $staticFlags)
$findItem = $catalogueType.GetMethod('Find', $instanceFlags)
$constructor = $dialogType.GetConstructors($instanceFlags) | Where-Object {
    $parameters = $_.GetParameters()
    $parameters.Count -eq 3 -and $parameters[0].ParameterType -eq $catalogueType -and $parameters[1].ParameterType -eq [string]
} | Select-Object -First 1
$cartField = $dialogType.GetField('cart', $instanceFlags)
$searchField = $dialogType.GetField('search', $instanceFlags)
$categoriesField = $dialogType.GetField('categoryTree', $instanceFlags)
$itemsField = $dialogType.GetField('items', $instanceFlags)
$setChatBinding = $dialogType.GetMethod('SetChatBinding', $instanceFlags)
$refreshCart = $dialogType.GetMethod('RefreshCart', $instanceFlags)
if ($null -eq $loadCatalogue -or $null -eq $findItem -or $null -eq $constructor -or
    $null -eq $cartField -or $null -eq $searchField -or $null -eq $categoriesField -or $null -eq $setChatBinding -or $null -eq $refreshCart) {
    throw 'This assembly does not contain the item selector. Supply a current canonical Test or Release build.'
}

function Initialize-PreviewControl([Windows.Forms.Control]$Control) {
    $null = $Control.Handle
    $Control.CreateControl()
    foreach ($child in $Control.Controls) { Initialize-PreviewControl $child }
    $Control.PerformLayout()
}

$previewTempRoot = [IO.Path]::GetFullPath([IO.Path]::GetTempPath()).TrimEnd([IO.Path]::DirectorySeparatorChar)
$previewProfileName = 'ScumMiniMap-SpawnCartPreview-' + [Guid]::NewGuid().ToString('N')
$previewProfile = [IO.Path]::GetFullPath((Join-Path $previewTempRoot $previewProfileName))
$dialog = $null
$bitmap = $null
$modifierDialog = $null
$modifierBitmap = $null
$kitDialog = $null
$kitBitmap = $null
$inactiveUi = $null
try {
    $inactiveUi = New-Object SpawnPreviewInactiveUi
    New-Item -ItemType Directory -Path $previewProfile | Out-Null
    $catalogue = $loadCatalogue.Invoke($null, [object[]]@())
    $cartPath = Join-Path $previewProfile 'spawn-cart.tsv'
    $dialog = $constructor.Invoke([object[]]@($catalogue, [string]$cartPath, $null))
    $dialog.TopMost = $false
    $dialog.ShowInTaskbar = $false
    $dialog.StartPosition = [Windows.Forms.FormStartPosition]::Manual
    $dialog.Location = New-Object Drawing.Point -32000,-32000
    $null = $setChatBinding.Invoke($dialog, [object[]]@('T'))

    $cart = $cartField.GetValue($dialog)
    $addItem = $cart.GetType().GetMethod('Add', $instanceFlags)
    if ($null -eq $addItem) { throw 'The current item selector does not expose its preview cart model.' }
    # These exact IDs are sourced from SCUM Database and resolved against this build's embedded catalogue.
    $sampleItems = @(
        @{ Id = 'Weapon_AK47'; Quantity = 1 },
        @{ Id = 'Magazine_AK47'; Quantity = 3 },
        @{ Id = 'Cal_7_62x39mm'; Quantity = 60 },
        @{ Id = 'Military_Backpack_01_01'; Quantity = 1 }
    )
    foreach ($sample in $sampleItems) {
        $item = $findItem.Invoke($catalogue, [object[]]@([string]$sample.Id))
        if ($null -eq $item) { throw ('The embedded catalogue is missing preview item ' + $sample.Id + '.') }
        $null = $addItem.Invoke($cart, [object[]]@([string]$sample.Id, [int]$sample.Quantity))
    }
    $setModifiers = $cart.GetType().GetMethod('SetModifiers', $instanceFlags)
    if ($null -ne $setModifiers) {
        $null = $setModifiers.Invoke($cart, [object[]]@('Weapon_AK47', 'Health 100 Weight 3.5'))
        $null = $setModifiers.Invoke($cart, [object[]]@('Cal_7_62x39mm', 'StackCount 20'))
    }
    $null = $refreshCart.Invoke($dialog, [object[]]@())
    $searchBox = $searchField.GetValue($dialog)
    $searchBox.Text = 'AK47'
    $categories = $categoriesField.GetValue($dialog)
    Initialize-PreviewControl $dialog
    foreach ($group in $categories.Nodes[0].Nodes) {
        $groupKey = $group.Tag.GetType().GetProperty('Key', $instanceFlags).GetValue($group.Tag, $null)
        if ($groupKey -eq 'group/weapons') {
            $group.Expand()
            foreach ($categoryNode in $group.Nodes) {
                $categoryKey = $categoryNode.Tag.GetType().GetProperty('Key', $instanceFlags).GetValue($categoryNode.Tag, $null)
                if ($categoryKey -eq 'category/weapons/weapon_ranged') { $categoryNode.Expand() }
            }
        }
    }
    $inactiveUi.Show($dialog)
    $items = $itemsField.GetValue($dialog)
    $setCollapsed = $items.GetType().GetMethod('SetGroupCollapsed', $instanceFlags)
    if ($null -ne $setCollapsed) {
        foreach ($group in $items.Groups) {
            if ($group.Name.StartsWith('ammunition/') -or $group.Name -eq 'attachment/general') {
                $null = $setCollapsed.Invoke($items,[object[]]@([string]$group.Name,$true))
            }
        }
    }
    [Windows.Forms.Application]::DoEvents()
    $bitmap = New-Object Drawing.Bitmap $dialog.Width,$dialog.Height
    $dialog.DrawToBitmap($bitmap, (New-Object Drawing.Rectangle 0,0,$dialog.Width,$dialog.Height))
    $bitmap.Save($previewOutput, [Drawing.Imaging.ImageFormat]::Png)
    Write-Output ('Saved item selector preview: {0} ({1}x{2})' -f $previewOutput,$bitmap.Width,$bitmap.Height)
    if ($KitOutputPath) {
        $databaseField = $dialogType.GetField('kitDatabase', $instanceFlags)
        $database = $databaseField.GetValue($dialog)
        $saveKit = $database.GetType().GetMethod('SaveKit', $instanceFlags)
        foreach ($kitName in @('AK-47 field kit', 'Patrol loadout', 'Range supplies')) {
            $null = $saveKit.Invoke($database, [object[]]@([string]$kitName, $cart, $false))
        }
        $kits = $database.GetType().GetMethod('ListKits', $instanceFlags).Invoke($database, [object[]]@())
        $kitType = $assembly.GetType('ScumMiniMap.SpawnKitDialog', $true)
        $kitConstructor = $kitType.GetConstructors($instanceFlags) | Select-Object -First 1
        $kitDialog = $kitConstructor.Invoke([object[]]@($false, $kits, [string]''))
        $inactiveUi.Show($kitDialog)
        $kitType.GetField('list', $instanceFlags).GetValue($kitDialog).SelectedIndex = 0
        [Windows.Forms.Application]::DoEvents()
        $kitBitmap = New-Object Drawing.Bitmap $kitDialog.Width,$kitDialog.Height
        $kitDialog.DrawToBitmap($kitBitmap, (New-Object Drawing.Rectangle 0,0,$kitDialog.Width,$kitDialog.Height))
        $kitOutput = [IO.Path]::GetFullPath($KitOutputPath)
        [IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($kitOutput)) | Out-Null
        $kitBitmap.Save($kitOutput, [Drawing.Imaging.ImageFormat]::Png)
        Write-Output ('Saved kit library preview: {0}' -f $kitOutput)
    }
    if ($ModifierOutputPath) {
        $modifierType = $assembly.GetType('ScumMiniMap.SpawnModifierDialog', $true)
        $modifierConstructor = $modifierType.GetConstructors($instanceFlags) | Where-Object {
            $parameters = $_.GetParameters()
            $parameters.Count -eq 2 -and $parameters[1].ParameterType -eq [string]
        } | Select-Object -First 1
        $modifierDialog = $modifierConstructor.Invoke([object[]]@('AK-47', 'Health 100 Uses 10 Weight 3.5'))
        $modifierDialog.TopMost = $false
        $modifierDialog.ShowInTaskbar = $false
        $modifierDialog.StartPosition = [Windows.Forms.FormStartPosition]::Manual
        $modifierDialog.Location = New-Object Drawing.Point -32000,-32000
        Initialize-PreviewControl $modifierDialog
        $inactiveUi.Show($modifierDialog)
        $modifierBitmap = New-Object Drawing.Bitmap $modifierDialog.Width,$modifierDialog.Height
        $modifierDialog.DrawToBitmap($modifierBitmap, (New-Object Drawing.Rectangle 0,0,$modifierDialog.Width,$modifierDialog.Height))
        $modifierBitmap.Save([IO.Path]::GetFullPath($ModifierOutputPath), [Drawing.Imaging.ImageFormat]::Png)
        Write-Output ('Saved modifier editor preview: {0}' -f [IO.Path]::GetFullPath($ModifierOutputPath))
    }
}
finally {
    if ($null -ne $kitBitmap) { $kitBitmap.Dispose() }
    if ($null -ne $kitDialog) { $kitDialog.Dispose() }
    if ($null -ne $modifierBitmap) { $modifierBitmap.Dispose() }
    if ($null -ne $modifierDialog) { $modifierDialog.Dispose() }
    if ($null -ne $bitmap) { $bitmap.Dispose() }
    if ($null -ne $dialog) { $dialog.Dispose() }
    if ($null -ne $inactiveUi) { $inactiveUi.Dispose() }
    if (Test-Path -LiteralPath $previewProfile) {
        $resolvedProfile = (Resolve-Path -LiteralPath $previewProfile).ProviderPath
        $resolvedTempRoot = (Resolve-Path -LiteralPath $previewTempRoot).ProviderPath.TrimEnd([IO.Path]::DirectorySeparatorChar)
        if (-not [string]::Equals($resolvedProfile,$previewProfile,[StringComparison]::OrdinalIgnoreCase) -or
            -not [string]::Equals([IO.Path]::GetDirectoryName($resolvedProfile),$resolvedTempRoot,[StringComparison]::OrdinalIgnoreCase) -or
            -not [string]::Equals([IO.Path]::GetFileName($resolvedProfile),$previewProfileName,[StringComparison]::Ordinal)) {
            throw 'Preview profile cleanup refused because its resolved path differs from the exact temporary directory created for this preview.'
        }
        Remove-Item -LiteralPath $resolvedProfile -Recurse -Force
    }
}
