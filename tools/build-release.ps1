param(
    [string]$Configuration = "Release"
)

$ErrorActionPreference = "Stop"

$root = Resolve-Path (Join-Path $PSScriptRoot "..")
$project = Join-Path $root "TextCrate.csproj"
$setupProject = Join-Path $root "setup\TextCrate.Setup.csproj"
$artifacts = Join-Path $root "artifacts"
$publish = Join-Path $artifacts "publish\TextCrate"
$setupPublish = Join-Path $artifacts "publish\TextCrate.Setup"
$installer = Join-Path $root "installer"

[xml]$projectXml = Get-Content $project
$version = $projectXml.Project.PropertyGroup.Version
if ([string]::IsNullOrWhiteSpace($version)) {
    $version = "0.1.0"
}

$portableZip = Join-Path $artifacts "TextCrate-v$version-win-x64-portable.zip"
$msiPath = Join-Path $artifacts "TextCrate-v$version-win-x64.msi"
$exePath = Join-Path $artifacts "TextCrate-v$version-win-x64-setup.exe"
$payloadZip = Join-Path $root "setup\Payload.zip"
$assemblyVersion = "$version.0"

Remove-Item -LiteralPath $artifacts -Recurse -Force -ErrorAction SilentlyContinue
New-Item -ItemType Directory -Force -Path $publish, $installer | Out-Null

dotnet publish $project -c $Configuration -r win-x64 --self-contained true -p:PublishSingleFile=false -p:DebugType=none -p:DebugSymbols=false -o $publish
Copy-Item -LiteralPath (Join-Path $root "README.md") -Destination (Join-Path $publish "README.md") -Force

Compress-Archive -Path (Join-Path $publish "*") -DestinationPath $portableZip -Force
Copy-Item -LiteralPath $portableZip -Destination $payloadZip -Force

dotnet publish $setupProject -c $Configuration -r win-x64 --self-contained true -p:PublishSingleFile=true -p:DebugType=none -p:DebugSymbols=false -p:Version=$version -p:AssemblyVersion=$assemblyVersion -p:FileVersion=$assemblyVersion -p:InformationalVersion=$version -o $setupPublish
Copy-Item -LiteralPath (Join-Path $setupPublish "TextCrate.Setup.exe") -Destination $exePath -Force

function ConvertTo-WixId([string]$prefix, [string]$value) {
    $sanitized = [Regex]::Replace($value, "[^A-Za-z0-9_]", "_")
    if ($sanitized.Length -gt 55) {
        $hash = [BitConverter]::ToString([Security.Cryptography.SHA1]::HashData([Text.Encoding]::UTF8.GetBytes($value))).Replace("-", "").Substring(0, 10)
        $sanitized = $sanitized.Substring(0, 44) + "_" + $hash
    }
    return "$prefix$sanitized"
}

function Escape-Xml([string]$value) {
    return [Security.SecurityElement]::Escape($value)
}

function Get-RelativePath([string]$basePath, [string]$targetPath) {
    $baseUri = [Uri]((Resolve-Path $basePath).Path.TrimEnd('\') + '\')
    $targetUri = [Uri](Resolve-Path $targetPath).Path
    return [Uri]::UnescapeDataString($baseUri.MakeRelativeUri($targetUri).ToString()).Replace('/', '\')
}

$dirMap = @{ "" = "INSTALLFOLDER" }
$directories = Get-ChildItem -LiteralPath $publish -Directory -Recurse | Sort-Object FullName
foreach ($dir in $directories) {
    $rel = Get-RelativePath $publish $dir.FullName
    $dirMap[$rel] = ConvertTo-WixId "DIR_" $rel
}

function Build-DirectoryXml([string]$path, [string]$indent) {
    $xml = ""
    foreach ($child in Get-ChildItem -LiteralPath $path -Directory | Sort-Object Name) {
        $rel = Get-RelativePath $publish $child.FullName
        $id = $dirMap[$rel]
        $name = Escape-Xml $child.Name
        $xml += "$indent<Directory Id=`"$id`" Name=`"$name`">`r`n"
        $xml += Build-DirectoryXml $child.FullName ($indent + "  ")
        $xml += "$indent</Directory>`r`n"
    }
    return $xml
}

$directoryXml = Build-DirectoryXml $publish "      "
$componentXml = ""
$componentRefs = ""
$index = 0
foreach ($file in Get-ChildItem -LiteralPath $publish -File -Recurse | Sort-Object FullName) {
    $rel = Get-RelativePath $publish $file.FullName
    $relDir = Split-Path $rel -Parent
    if ($relDir -eq ".") { $relDir = "" }
    $dirId = $dirMap[$relDir]
    $componentId = "CMP_$index"
    $fileId = "FIL_$index"
    $source = Escape-Xml $file.FullName
    $componentXml += "    <Component Id=`"$componentId`" Directory=`"$dirId`" Guid=`"*`">`r`n"
    $componentXml += "      <File Id=`"$fileId`" Source=`"$source`" KeyPath=`"yes`" />`r`n"
    $componentXml += "    </Component>`r`n"
    $componentRefs += "      <ComponentRef Id=`"$componentId`" />`r`n"
    $index++
}

$productWxs = Join-Path $installer "TextCrate.generated.wxs"
$iconPath = Escape-Xml (Join-Path $root "assets\icon.ico")
$upgradeCode = "f87c5f2b-4771-4f03-b86b-70dff58f5150"

@"
<Wix xmlns="http://wixtoolset.org/schemas/v4/wxs">
  <Package Name="TextCrate" Manufacturer="Goblin Rules" Version="$version" UpgradeCode="$upgradeCode" Scope="perMachine">
    <MajorUpgrade DowngradeErrorMessage="A newer version of TextCrate is already installed." />
    <MediaTemplate EmbedCab="yes" />
    <Icon Id="TextCrateIcon" SourceFile="$iconPath" />
    <Property Id="ARPPRODUCTICON" Value="TextCrateIcon" />

    <StandardDirectory Id="ProgramFiles64Folder">
      <Directory Id="INSTALLFOLDER" Name="TextCrate">
$directoryXml      </Directory>
    </StandardDirectory>

    <StandardDirectory Id="ProgramMenuFolder">
      <Directory Id="ApplicationProgramsFolder" Name="TextCrate" />
    </StandardDirectory>

    <ComponentGroup Id="PublishedFiles">
$componentXml    </ComponentGroup>

    <Component Id="ApplicationShortcut" Directory="ApplicationProgramsFolder" Guid="*">
      <Shortcut Id="ApplicationStartMenuShortcut" Name="TextCrate" Description="Launch TextCrate" Target="[INSTALLFOLDER]TextCrate.exe" WorkingDirectory="INSTALLFOLDER" />
      <RemoveFolder Id="ApplicationProgramsFolder" On="uninstall" />
      <RegistryValue Root="HKCU" Key="Software\Goblin Rules\TextCrate" Name="installed" Type="integer" Value="1" KeyPath="yes" />
    </Component>

    <Feature Id="MainFeature" Title="TextCrate" Level="1">
      <ComponentGroupRef Id="PublishedFiles" />
      <ComponentRef Id="ApplicationShortcut" />
    </Feature>
  </Package>
</Wix>
"@ | Set-Content -LiteralPath $productWxs -Encoding UTF8

wix build $productWxs -arch x64 -out $msiPath
if ($LASTEXITCODE -ne 0) { throw "WiX MSI build failed." }

Write-Host "Built:"
Write-Host "  $portableZip"
Write-Host "  $msiPath"
Write-Host "  $exePath"
