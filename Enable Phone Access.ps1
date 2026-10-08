$ErrorActionPreference = 'Stop'
$boutiqueRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
try {
    $boutiquePython = 'C:\Users\BharathChintalapani\AppData\Local\Programs\Python\Python313\python.exe'
    $boutiqueRule = Get-NetFirewallRule -DisplayName 'Nandita Handlooms - private Wi-Fi' -ErrorAction SilentlyContinue
    if (-not $boutiqueRule) {
        New-NetFirewallRule -DisplayName 'Nandita Handlooms - private Wi-Fi' -Direction Inbound -Action Allow -Protocol TCP -LocalPort 8765 -RemoteAddress LocalSubnet -InterfaceAlias 'Wi-Fi' -Program $boutiquePython -Profile Private | Out-Null
        # The detached launcher uses pythonw, so allow that executable under the same narrow scope.
        New-NetFirewallRule -DisplayName 'Nandita Handlooms background - private Wi-Fi' -Direction Inbound -Action Allow -Protocol TCP -LocalPort 8765 -RemoteAddress LocalSubnet -InterfaceAlias 'Wi-Fi' -Program 'C:\Users\BharathChintalapani\AppData\Local\Programs\Python\Python313\pythonw.exe' -Profile Private | Out-Null
    }
    Set-NetConnectionProfile -InterfaceAlias 'Wi-Fi' -NetworkCategory Private
    'Private Wi-Fi access enabled for the boutique app on TCP 8765, local subnet only.' | Set-Content -LiteralPath (Join-Path $boutiqueRoot 'phone-setup.log')
} catch {
    $_.Exception.Message | Set-Content -LiteralPath (Join-Path $boutiqueRoot 'phone-setup.log')
    exit 1
}
