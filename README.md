# Wind Suhu

Aplikasi Windows untuk melihat suhu CPU, pemakaian RAM, dan kapasitas disk sistem langsung di taskbar. Tiga ikon angka muncul di **system tray** (sebelah jam); klik ikon mana pun untuk membuka panel detail. Klik kanan untuk keluar.

## Jalankan

Butuh Windows 10/11 dan .NET 8 SDK untuk membangun aplikasi.

```powershell
dotnet run --project WindSuhu/WindSuhu.csproj
```

Untuk membuat aplikasi yang bisa dipindahkan ke komputer Windows x64 tanpa memasang .NET Runtime:

```powershell
dotnet publish WindSuhu/WindSuhu.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -o dist
```

Jalankan `dist/WindSuhu.exe`. Windows dapat menaruh ikon baru di menu ikon tersembunyi (`^`). Seret ketiga ikon ke sebelah jam agar angkanya selalu terlihat.

## Arti angka

- **°C**: suhu tertinggi dari sensor temperatur CPU yang terbaca. Jika tidak tersedia, tampil `—`.
- **RAM**: persentase memori fisik terpakai menurut Windows.
- **DISK**: persentase kapasitas terpakai pada drive tempat Windows terpasang; ini bukan aktivitas baca/tulis disk.

Pembacaan diperbarui tiap 2 detik. Sensor CPU memakai [LibreHardwareMonitorLib](https://github.com/LibreHardwareMonitor/LibreHardwareMonitor). Jika sensor mengembalikan `0 °C`, aplikasi menampilkannya sebagai tidak tersedia (`—`). Untuk membaca sensor CPU, pasang driver [PawnIO resmi](https://pawnio.eu/) (pilih edisi resmi yang ditandatangani), tutup aplikasi, lalu jalankan kembali sebagai Administrator. Menu klik kanan ikon menyediakan tautan unduhan dan opsi menjalankan ulang sebagai Administrator. RAM dan disk tetap dapat tampil tanpa driver maupun hak Administrator. Aplikasi tidak mengendalikan kipas atau mengirim data.

Versi Windows menggunakan ikon system tray karena Windows tidak menyediakan area teks menu bar seperti macOS. Setiap angka menempati satu ikon kecil.

## Diagnostik sensor

Jika suhu tetap `—` setelah dijalankan sebagai Administrator, buka PowerShell sebagai Administrator di folder proyek lalu jalankan:

```powershell
dotnet run --project diagnostics/SensorProbe/SensorProbe.csproj
```

Perintah ini menampilkan nama CPU dan nilai mentah semua sensor suhu CPU yang ditemukan, sehingga dapat dibedakan antara sensor yang tidak tersedia dan sensor yang melaporkan `0 °C`.
