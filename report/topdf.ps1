param([string]$in, [string]$out)
# Конвертація .docx → .pdf через Microsoft Word (COM).
$w = New-Object -ComObject Word.Application
$w.Visible = $false
$d = $w.Documents.Open($in, $false, $true)
$d.SaveAs([ref]$out, [ref]17)
$n = $d.ComputeStatistics(2)
$d.Close($false); $w.Quit()
Write-Output "pages=$n"
