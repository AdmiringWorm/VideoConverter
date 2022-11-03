$ErrorActionPreference = 'Stop';
$toolsDir   = "$(Split-Path -parent $MyInvocation.MyCommand.Definition)"

$packageArgs = @{
	packageName   = $env:ChocolateyPackageName
	fileType      = 'exe'
	file64        = "$toolsDir\{{FILE_NAME}}"

	softwareName  = 'VideoConverter*'

	silentArgs    = "/VERYSILENT /SUPPRESSMSGBOXES /NORESTART /SP-"
	validExitCodes= @(0, 3010, 1641)
}

Install-ChocolateyInstallPackage @packageArgs
