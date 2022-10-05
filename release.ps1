$array = @("AI", "HS2")

$dir = $PSScriptRoot + "\bin\"

$copy = $dir + "\copy\BepInEx"

Remove-Item -Force -Path ($copy) -Recurse -ErrorAction SilentlyContinue
New-Item -ItemType Directory -Force -Path ($copy + "\plugins")
New-Item -ItemType Directory -Force -Path ($dir + "out\")

function CreateZip ($element) {
    Remove-Item -Force -Path ($copy) -Recurse
    New-Item -ItemType Directory -Force -Path ($copy + "\plugins")

    $lp = $copy + "\plugins\"
    $dp = $PSScriptRoot + "\src\" + $element + "\bin\Release\";
    $cp = New-Item -ItemType Directory -Force -Path ($lp + "..\core")
    Copy-Item -Path ($dp + $element + "_CharaLoader.*") -Destination ($lp) -Recurse -Force
    Copy-Item -Path ($dp + "System.Buffers.*") -Destination ($cp) -Recurse -Force

    try {
        $ver = "r" + (Get-ChildItem -Path ($copy) -Filter ($element + "_*.dll") -Recurse -Force)[0].VersionInfo.FileVersion.ToString()
    }
    catch {
        $ver = "r" + (Get-ChildItem -Path ($copy) -Filter ("*.dll") -Recurse -Force)[0].VersionInfo.FileVersion.ToString()
    }

    Compress-Archive -Path $copy -Force -CompressionLevel "Optimal" -DestinationPath ($dir + "out\" + $element + "_CharaLoader_" + $ver + ".zip")
}

foreach ($element in $array) {
    try {
        CreateZip ($element)
    }
    catch {
        # retry
        CreateZip ($element)
    }
}

Remove-Item -Force -Path ($dir + "\copy") -Recurse
