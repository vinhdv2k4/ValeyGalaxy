$ErrorActionPreference = 'Stop'
$projectRoot = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot '../..')).Path
$dataRoot = Join-Path $projectRoot 'Assets/Content/Data'
$rows = [System.Collections.Generic.List[object]]::new()

$sources = @(
    @{ Name = 'Objects'; Kind = 'item_candidate'; NameField = 'none' },
    @{ Name = 'BigCraftables'; Kind = 'item_candidate'; NameField = 'none' },
    @{ Name = 'Tools'; Kind = 'item_candidate'; NameField = 'none' },
    @{ Name = 'Weapons'; Kind = 'item_candidate'; NameField = 'none' },
    @{ Name = 'Boots'; Kind = 'item_candidate'; NameField = 'slash' },
    @{ Name = 'hats'; Kind = 'item_candidate'; NameField = 'slash' },
    @{ Name = 'Furniture'; Kind = 'item_candidate'; NameField = 'slash' },
    @{ Name = 'Pants'; Kind = 'item_candidate'; NameField = 'none' },
    @{ Name = 'Shirts'; Kind = 'item_candidate'; NameField = 'none' },
    @{ Name = 'Trinkets'; Kind = 'item_candidate'; NameField = 'none' },
    @{ Name = 'Crops'; Kind = 'farm_reference'; NameField = 'none' },
    @{ Name = 'Fish'; Kind = 'farm_reference'; NameField = 'slash' },
    @{ Name = 'FruitTrees'; Kind = 'farm_reference'; NameField = 'localized' },
    @{ Name = 'FarmAnimals'; Kind = 'farm_reference'; NameField = 'key' },
    @{ Name = 'GiantCrops'; Kind = 'farm_reference'; NameField = 'key' },
    @{ Name = 'CraftingRecipes'; Kind = 'recipe_reference'; NameField = 'key' },
    @{ Name = 'CookingRecipes'; Kind = 'recipe_reference'; NameField = 'key' },
    @{ Name = 'Fences'; Kind = 'item_support_reference'; NameField = 'none' },
    @{ Name = 'FloorsAndPaths'; Kind = 'item_support_reference'; NameField = 'none' },
    @{ Name = 'Machines'; Kind = 'item_support_reference'; NameField = 'none' },
    @{ Name = 'Shops'; Kind = 'shop_definition'; NameField = 'none' }
)

foreach ($source in $sources) {
    $filename = "$($source.Name).json"
    $data = Get-Content -LiteralPath (Join-Path $dataRoot $filename) -Raw |
        ConvertFrom-Json -AsHashtable
    foreach ($key in $data.Keys) {
        $value = $data[$key]
        $name = ''
        $status = 'id_only'
        switch ($source.NameField) {
            'slash' {
                if ($value -is [string] -and $value.Length -gt 0) {
                    $name = ($value -split '/', 2)[0]
                    $status = 'named_with_source_fields'
                }
            }
            'localized' {
                if ($value -is [System.Collections.IDictionary] -and $value.Contains('DisplayName')) {
                    $name = [string]$value['DisplayName']
                    $status = 'localized_name_reference'
                }
            }
            'key' {
                $name = [string]$key
                $status = if ($source.Kind -eq 'recipe_reference') {
                    'recipe_name_and_source_fields'
                } else { 'source_name_only' }
            }
        }
        $rows.Add([pscustomobject]@{
            kind = $source.Kind
            family = $source.Name
            source_id = [string]$key
            display_name = $name
            evidence_status = $status
            source_file = "Assets/Content/Data/$filename"
        })
    }
}

$tailoring = Get-Content -LiteralPath (Join-Path $dataRoot 'TailoringRecipes.json') -Raw |
    ConvertFrom-Json -AsHashtable
for ($i = 0; $i -lt $tailoring.Count; $i++) {
    $id = if ($tailoring[$i].Contains('Id')) { [string]$tailoring[$i]['Id'] } else { "row_$i" }
    $rows.Add([pscustomobject]@{
        kind = 'recipe_reference'; family = 'TailoringRecipes'; source_id = $id
        display_name = ''; evidence_status = 'recipe_id_only'
        source_file = 'Assets/Content/Data/TailoringRecipes.json'
    })
}

$onlineRoot = Join-Path $projectRoot 'Assets/Resources/OnlineItems'
foreach ($asset in Get-ChildItem -LiteralPath $onlineRoot -Filter '*.asset') {
    $yaml = Get-Content -LiteralPath $asset.FullName -Raw
    $id = [regex]::Match($yaml, '(?m)^  itemId: (.+)\r?$').Groups[1].Value.Trim()
    $name = [regex]::Match($yaml, '(?m)^  displayName: (.+)\r?$').Groups[1].Value.Trim()
    $rows.Add([pscustomobject]@{
        kind = 'online_implemented'; family = 'OnlineItems'; source_id = $id
        display_name = $name; evidence_status = 'unity_asset'
        source_file = "Assets/Resources/OnlineItems/$($asset.Name)"
    })
}

$output = Join-Path $PSScriptRoot 'item-source-index.csv'
$rows | Sort-Object family, source_id | Export-Csv -LiteralPath $output -NoTypeInformation -Encoding utf8
Write-Output "Exported $($rows.Count) source records to $output"

# Static references in shop stock code are evidence of a possible offer, not a complete shop inventory.
$shopRefs = [System.Collections.Generic.List[object]]::new()
$codeRoot = Join-Path $projectRoot 'Assets/Scripts/GalaxyPlay'
$matches = & rg -n -i 'stock.*new (gamesource\.)?object\(' $codeRoot -g '*.cs'
foreach ($entry in $matches) {
    $location = [regex]::Match($entry, '^(?<path>.+?):(?<line>\d+):(?<statement>.*)$')
    if (-not $location.Success) { continue }
    $object = [regex]::Match($location.Groups['statement'].Value,
        'new (?:GameSource\.)?Object\((?:Vector2\.Zero,\s*)?(?<id>\d+)',
        [System.Text.RegularExpressions.RegexOptions]::IgnoreCase)
    if (-not $object.Success) { continue }
    $path = $location.Groups['path'].Value.Replace($projectRoot + [IO.Path]::DirectorySeparatorChar, '').Replace('\', '/')
    $shopRefs.Add([pscustomobject]@{
        item_source_id = $object.Groups['id'].Value
        code_file = $path
        line = [int]$location.Groups['line'].Value
        evidence_status = 'literal_stock_reference_only'
    })
}
$shopOutput = Join-Path $PSScriptRoot 'shop-code-references.csv'
$shopRefs | Sort-Object code_file, line | Export-Csv -LiteralPath $shopOutput -NoTypeInformation -Encoding utf8
Write-Output "Exported $($shopRefs.Count) explicit numeric stock references to $shopOutput"
