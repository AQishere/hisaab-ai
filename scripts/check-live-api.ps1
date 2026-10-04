$ErrorActionPreference = 'Stop'
$base = 'http://localhost:5069'
$settings = Get-Content 'QadoosFlourMill/Kratix.ERP/appsettings.Development.json' -Raw | ConvertFrom-Json
$owner = $settings.DemoUsers | Where-Object Role -eq 'Owner' | Select-Object -First 1
$login = Invoke-RestMethod -Method Post -Uri "$base/api/auth/login" -ContentType 'application/json' -Body (@{ username = $owner.Username; password = $owner.Password } | ConvertTo-Json)
$headers = @{ Authorization = "Bearer $($login.token)" }
$lists = @{
  suppliers = @('suppliers','supplierID'); customers = @('customers','customerID');
  products = @('products','productID'); warehouses = @('warehouses','warehouseID');
  purchaseorders = @('purchaseorders','poid'); inboundreceipts = @('inboundreceipts','receiptID');
  productionbatches = @('productionbatches','batchID'); salesorders = @('salesorders','salesOrderID');
  invoices = @('invoices','invoiceID'); payments = @('payments','paymentID');
  users = @('users','userID'); inventory = @('inventory','inventoryID')
}
$cache = @{}
foreach ($name in $lists.Keys) {
  $cache[$name] = Invoke-RestMethod -Uri "$base/api/$name" -Headers $headers
}
$routes = Get-Content 'QadoosFlourMill/Kratix.ERP/API_ENDPOINTS.md' |
  ForEach-Object { if ($_ -match '^- GET (/api/[^\s]+)') { $Matches[1] } } |
  ForEach-Object { ($_ -split '\?')[0] } | Sort-Object -Unique
$results = foreach ($route in $routes) {
  $path = $route -replace '^/api/', ''
  if ($path -match '^payments/invoice/\{invoiceId\}$') {
    $item = $cache.invoices | Select-Object -First 1
    if (!$item) { [PSCustomObject]@{ Method='GET'; Path=$route; Result='SKIP (no invoice to reference)' }; continue }
    $path = "payments/invoice/$($item.invoiceID)"
  } elseif ($path -match '^([^/]+)/\{id\}') {
    $resource = $Matches[1]
    if (!$lists.ContainsKey($resource)) { [PSCustomObject]@{ Method='GET'; Path=$route; Result='UNMAPPED' }; continue }
    $item = $cache[$resource] | Select-Object -First 1
    if (!$item) { [PSCustomObject]@{ Method='GET'; Path=$route; Result='SKIP (no record to reference)' }; continue }
    $id = $item.($lists[$resource][1])
    $path = $path.Replace('{id}', [string]$id)
  } else { $resource = ($path -split '/')[0] }
  try {
    $null = Invoke-RestMethod -Uri "$base/api/$path" -Headers $headers
    [PSCustomObject]@{ Method='GET'; Path=$route; Result='200 OK' }
  } catch {
    $status = $_.Exception.Response.StatusCode.value__
    $detail = if ($status) { $reader = [System.IO.StreamReader]::new($_.Exception.Response.GetResponseStream()); $body = $reader.ReadToEnd(); $reader.Dispose(); "$status FAILED: $body" } else { "ERROR: $($_.Exception.Message)" }
    [PSCustomObject]@{ Method='GET'; Path=$route; Result=$detail }
  }
}
$results | Format-List Method,Path,Result
$failed = @($results | Where-Object Result -Match 'FAILED|ERROR|UNMAPPED')
Write-Output "GET routes checked: $($results.Count); failed: $($failed.Count); skipped for missing sample data: $(@($results | Where-Object Result -Like 'SKIP*').Count)."
if ($failed.Count) { exit 1 }
