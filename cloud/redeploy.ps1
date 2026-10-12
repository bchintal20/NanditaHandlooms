# Redeploy code only; this does not provision infrastructure or import records.
$ErrorActionPreference='Stop'
$repo=Split-Path $PSScriptRoot -Parent
Set-Location $repo
$env:AZURE_CONFIG_DIR=Join-Path $repo '.azure-personal'
$subscription='ef6358bf-6180-4a31-a0e2-97b0a66dc438'
function Invoke-DeployAz {
 param([string]$Label,[string[]]$Arguments)
 $audit=Join-Path $repo '.artifacts/code-deploy-audit.log'
 Add-Content -LiteralPath $audit -Value "$([DateTime]::UtcNow.ToString('o')) started $Label"
 $result=& az @Arguments 2>$null
 $code=$LASTEXITCODE
 Add-Content -LiteralPath $audit -Value "$([DateTime]::UtcNow.ToString('o')) $(if($code -eq 0){'succeeded'}else{'failed'}) $Label exit=$code"
 if($code -ne 0){throw "Azure command failed: $Label"}
 return $result
}
New-Item -ItemType Directory -Path (Join-Path $repo '.artifacts') -Force | Out-Null
$accountText=Invoke-DeployAz 'Verify personal subscription' @('account','show','-o','json')
$account=($accountText -join "`n") | ConvertFrom-Json
if($account.id -ne $subscription -or $account.tenantId -ne '99fe16c7-0092-41c3-8ca5-a7e3f7031788'){throw 'Expected personal Azure subscription; deployment stopped.'}
python cloud/build_ui.py
if($LASTEXITCODE -ne 0){throw 'Frontend staging failed'}
dotnet publish cloud/Api/Boutique.Api.csproj -c Release -o .artifacts/api
if($LASTEXITCODE -ne 0){throw 'API publish failed'}
$cli=Join-Path $repo '.artifacts/deploy-tools/node_modules/.bin/swa.cmd'
if(-not(Test-Path $cli)){
 npm install --prefix .artifacts/deploy-tools @azure/static-web-apps-cli@2.0.10 --no-audit --no-fund
 if($LASTEXITCODE -ne 0){throw 'Deployment CLI installation failed'}
}
$stage=Join-Path 'C:\temp' ('nandita-code-'+[guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path "$stage/site","$stage/api" -Force | Out-Null
Copy-Item '.artifacts/site/*' "$stage/site" -Recurse
Copy-Item '.artifacts/api/*' "$stage/api" -Recurse
if(@(Get-ChildItem "$stage/site" -File).Count -ne 4){throw 'Unexpected public files'}
try {
 $token=Invoke-DeployAz 'Retrieve SWA token (output suppressed)' @('staticwebapp','secrets','list','--subscription',$subscription,'-n','swa-nandita-dev-cbbf','-g','rg-nandita-dev-cbbf','--query','properties.apiKey','-o','tsv')
 $env:SWA_CLI_DEPLOYMENT_TOKEN=($token -join '').Trim()
 & $cli deploy "$stage/site" --api-location "$stage/api" --api-language dotnetisolated --api-version 10.0 --app-name swa-nandita-dev-cbbf --env production
 if($LASTEXITCODE -ne 0){throw 'Code deployment failed'}
 $resolved=[IO.Path]::GetFullPath($stage)
 if($resolved -notmatch '^C:\\temp\\nandita-code-[a-f0-9]{32}$'){throw 'Unexpected cleanup path'}
 Remove-Item -LiteralPath $resolved -Recurse -Force
} finally {Remove-Item Env:SWA_CLI_DEPLOYMENT_TOKEN -ErrorAction SilentlyContinue; $token=$null}
