# ѕростой детектор циклов ProjectReference
# «апуск: pwsh ./tools/detect-csproj-cycles.ps1

$root = Get-Location
$projects = Get-ChildItem -Path $root -Recurse -Filter *.csproj | ForEach-Object { $_.FullName }

# —обираем граф: проект -> список целевых проектов (полные пути)
$graph = @{}
foreach ($p in $projects) {
	[xml]$xml = Get-Content -Path $p
	$refs = @()
	$ns = $xml.Project
	if ($ns -ne $null) {
		$projRefs = $xml.SelectNodes("//ProjectReference")
		foreach ($r in $projRefs) {
			$inc = $r.Include
			if ($inc) {
				$refPath = Resolve-Path -Path (Join-Path (Split-Path $p) $inc) -ErrorAction SilentlyContinue
				if ($refPath) { $refs += $refPath.Path } else { $refs += (Join-Path (Split-Path $p) $inc) }
			}
		}
	}
	$graph[$p] = $refs
}

# DFS дл€ поиска циклов
$visited = @{}
$stack = @()
$found = $false

function Visit($node, [ref]$stackList, [ref]$visitedMap) {
	if ($visitedMap.Value.ContainsKey($node) -and $visitedMap.Value[$node] -eq 'visiting') {
		# нашли цикл Ч распечатать путь
		$idx = $stackList.Value.IndexOf($node)
		if ($idx -ge 0) {
			Write-Host "Cycle detected:" -ForegroundColor Yellow
			for ($i = $idx; $i -lt $stackList.Value.Count; $i++) {
				Write-Host "  -> " $stackList.Value[$i]
			}
			Write-Host "  -> " $node
		} else {
			Write-Host "Cycle includes: $node"
		}
		$global:found = $true
		return
	}
	if ($visitedMap.Value.ContainsKey($node) -and $visitedMap.Value[$node] -eq 'done') { return }

	$visitedMap.Value[$node] = 'visiting'
	$stackList.Value.Add($node)

	if ($graph.ContainsKey($node)) {
		foreach ($n in $graph[$node]) {
			if (Test-Path $n) { Visit $n ([ref]$stackList) ([ref]$visitedMap) } else {
				# игнорируем внешние/несуществующие ссылки
			}
		}
	}

	$stackList.Value.RemoveAt($stackList.Value.Count - 1)
	$visitedMap.Value[$node] = 'done'
}

foreach ($proj in $graph.Keys) {
	if (-not $visited.ContainsKey($proj)) {
		Visit $proj ([ref]([System.Collections.ArrayList]@())) ([ref]( @{ } ))
	}
}

if (-not $found) { Write-Host "No cycles detected." -ForegroundColor Green } else { Write-Host "Fix the listed cycles (remove or conditionally change one ProjectReference in each cycle)." -ForegroundColor Red }
