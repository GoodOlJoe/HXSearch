#unzips WT helix patches, creates a folder with the needed files in it, and also moves the zip itself into that folder
# in the ISE, set the working directory to the folder containing the zip files you want to proces
# it will also create the patch folder in this same directory (prepended with "not loaded" so we know we haven't
# put it on the Helix yet
$tempDir = ".\temp"
Get-ChildItem *.zip | 
ForEach-Object {
    $fiZip = $_
    $targetDir = "$($fizip.Directory)\not loaded $($fizip.BaseName)"
    if ( Test-Path $tempDir ) { Remove-Item $tempDir -force -Recurse }
    if ( Test-Path $targetDir ) { Remove-Item $targetDir -force -Recurse }
    New-Item $targetDir -ItemType Directory 
    Expand-Archive -Path $fiZip.FullName -DestinationPath $tempDir
    Remove-Item "$tempDir\__MACOSX" -Recurse -Force
    Get-ChildItem $tempDir -Recurse -Include @("*.pdf","*.wav","*.hlx") |
    ForEach-Object {
        $_.Name
        Move-item $_ -destination $targetDir
    }
    Move-Item $fiZip.FullName -Destination $targetDir
 }
 if ( Test-Path $tempDir ) { Remove-Item $tempDir -force -Recurse }
