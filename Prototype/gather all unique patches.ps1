using namespace System.IO

$scriptRoot = $PSScriptRoot

$files = Get-ChildItem $scriptRoot -Recurse -Include "*.hlx"

$all = @{}

$files |
ForEach-Object {
    $fi = $_
    if (! $all.ContainsKey($fi.Name)) {
        # first level key is the file name without path
        $all.Add($fi.Name, @{})
    }
    if (! $all[$fi.Name].ContainsKey($fi.Length)) {
        # second level key is the file size, ignoring multiples of the same name/size
        $all[$fi.Name].Add($fi.Length, $fi)
    }
}

# now the dictionary has one record for every unique combination of file
# name and file size, copy them
$targetDir = [System.IO.Path]::Combine($scriptRoot, "All Unique Patches", (Get-Date).ToString("yyyy MM dd"))
if ( Test-Path $targetDir ) {
    Remove-Item $targetDir -Recurse
}
New-Item -ItemType Directory -Path $targetDir
$all.Values |
ForEach-Object {
    $FIs = $_
    $seq = 0
    $FIs.Values |
    ForEach-Object {
        $fi = $_
        $targetName = $fi.BaseName
        if ($seq -gt 0) {
            $targetName = "$targetName ($seq)"
        }
        $targetName = "$targetName$($fi.Extension)"
        Copy-Item $fi -Destination  ([System.IO.Path]::Combine($targetDir, $targetName ))
        $seq++
    }

}
