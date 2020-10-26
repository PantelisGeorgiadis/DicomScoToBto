$ErrorActionPreference = 'Stop'

Try {
	# Download VsWhere
	# This will help to locate the VS2019 C# build tools path
	$tempDir = [System.IO.Path]::GetTempPath()
	$vsWherePath = Join-Path $tempDir 'vswhere.exe'
	$vsWhereUrl = 'https://github.com/microsoft/vswhere/releases/download/2.8.4/vswhere.exe'
    (New-Object System.Net.WebClient).DownloadFile($vsWhereUrl, $vsWherePath)

	# Locate VS2019 MsBuild
	$msBuildPath = & $vsWherePath -Latest -Requires Microsoft.Component.MSBuild -Find MSBuild\**\Bin\MSBuild.exe | Select-Object -First 1
	If ([String]::IsNullOrEmpty($msBuildPath)) {
		Throw 'MSBuild path is null or empty'
	}
	Write-Host ("MsBuild path: $($msBuildPath)")

	# Project name and folder
	$projectFolder = 'DicomScoToBto'
	$projectFile = 'DicomScoToBto.csproj'

	# Project path
	$solutionPath = Split-Path -path $((Get-Location).Path) -Parent
	$project = $solutionPath, $projectFolder, $projectFile -Join [IO.Path]::DirectorySeparatorChar

	# Clean up
	Invoke-Expression "& '$msBuildPath' $project /t:Clean /p:Configuration=Release"

	# Restore Packages
	Invoke-Expression "& '$msBuildPath' $project /t:Restore"

	# Build
	Invoke-Expression "& '$msBuildPath' $project /p:Configuration=Release"
} Catch {
    Write-Host $Error[0].Exception
}
