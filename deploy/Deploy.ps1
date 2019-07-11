<#
.SYNOPSIS

Helper script to deploy the infrastructure to Azure.
This will deploy all necessary resources to run the app.

#>

param(
    [string] $ResourceGroupName,
    [string] $ResourceGroupLocation = "westeurope"
)
$ErrorActionPreference = "Stop"

if (!$ResourceGroupName) {
    throw "Resourcegroup name must be set"
}
if ($ResourceGroupName.length -gt 23) {
    throw "Resourcegroup name $ResourceGroupName is too long. Keyvault is limited to 23 characters"
}

$ResourceGroup = Get-AzResourceGroup -Name $ResourceGroupName -ErrorAction SilentlyContinue
if ($ResourceGroup -eq $null) {
    Write-Output "Creating resourcegroup $ResourceGroupName in $ResourceGroupLocation"
    New-AzResourceGroup -Name $ResourceGroupName -Location $ResourceGroupLocation -Force -ErrorAction Stop
}

Write-Output "Deploying resourcegroup $ResourceGroupName"

$templateFile = Join-Path $PSScriptRoot "deploy.json"
$parameters = @{ 
}

New-AzResourceGroupDeployment `
    -ResourceGroupName $ResourceGroupName `
    -TemplateFile $templateFile `
    -TemplateParameterObject $parameters
