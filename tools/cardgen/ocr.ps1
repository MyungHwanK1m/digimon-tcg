# Windows 내장 OCR(한국어)로 카드 이미지의 글자를 줄 단위로 읽는다. (선택) 영역을 비율로 잘라서 읽을 수 있다.
param([string]$Path, [double]$X = 0, [double]$Y = 0, [double]$W = 1, [double]$H = 1, [double]$Scale = 2.0)
Add-Type -AssemblyName System.Runtime.WindowsRuntime
Add-Type -AssemblyName System.Drawing
$null = [Windows.Media.Ocr.OcrEngine, Windows.Foundation, ContentType = WindowsRuntime]
$null = [Windows.Graphics.Imaging.SoftwareBitmap, Windows.Foundation, ContentType = WindowsRuntime]
$null = [Windows.Storage.StorageFile, Windows.Storage, ContentType = WindowsRuntime]
$null = [Windows.Globalization.Language, Windows.Globalization, ContentType = WindowsRuntime]
$asTask = ([System.WindowsRuntimeSystemExtensions].GetMethods() | Where-Object { $_.Name -eq 'AsTask' -and $_.GetParameters().Count -eq 1 -and $_.GetParameters()[0].ParameterType.Name -eq 'IAsyncOperation`1' })[0]
function Await($op, [Type]$t) { $task = $asTask.MakeGenericMethod($t).Invoke($null, @($op)); $task.Wait(); $task.Result }

# 영역 자르기 + 확대 (작은 글자 인식률 향상) → 임시 PNG
$src = [System.Drawing.Image]::FromFile($Path)
$rx = [int]($src.Width * $X); $ry = [int]($src.Height * $Y); $rw = [int]($src.Width * $W); $rh = [int]($src.Height * $H)
$bmp = New-Object System.Drawing.Bitmap ([int]($rw * $Scale)), ([int]($rh * $Scale))
$g = [System.Drawing.Graphics]::FromImage($bmp); $g.InterpolationMode = 'HighQualityBicubic'
$g.DrawImage($src, (New-Object System.Drawing.Rectangle 0, 0, $bmp.Width, $bmp.Height), (New-Object System.Drawing.Rectangle $rx, $ry, $rw, $rh), 'Pixel')
$tmp = [System.IO.Path]::Combine([System.IO.Path]::GetTempPath(), "ocr_" + [guid]::NewGuid().ToString("N") + ".png")
$bmp.Save($tmp, [System.Drawing.Imaging.ImageFormat]::Png); $g.Dispose(); $bmp.Dispose(); $src.Dispose()

$file = Await ([Windows.Storage.StorageFile]::GetFileFromPathAsync($tmp)) ([Windows.Storage.StorageFile])
$stream = Await ($file.OpenAsync([Windows.Storage.FileAccessMode]::Read)) ([Windows.Storage.Streams.IRandomAccessStream])
$decoder = Await ([Windows.Graphics.Imaging.BitmapDecoder]::CreateAsync($stream)) ([Windows.Graphics.Imaging.BitmapDecoder])
$sbmp = Await ($decoder.GetSoftwareBitmapAsync()) ([Windows.Graphics.Imaging.SoftwareBitmap])
$engine = [Windows.Media.Ocr.OcrEngine]::TryCreateFromLanguage([Windows.Globalization.Language]::new("ko"))
$result = Await ($engine.RecognizeAsync($sbmp)) ([Windows.Media.Ocr.OcrResult])
$stream.Dispose()
Remove-Item $tmp -ErrorAction SilentlyContinue
$result.Lines | ForEach-Object { $_.Text }
