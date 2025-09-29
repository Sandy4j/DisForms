# 💬 DisForms - Local Chat Application

<div align="center">

![C#](https://img.shields.io/badge/C%23-239120?style=for-the-badge&logo=c-sharp&logoColor=white)
![.NET](https://img.shields.io/badge/.NET-512BD4?style=for-the-badge&logo=dotnet&logoColor=white)
![WPF](https://img.shields.io/badge/WPF-512BD4?style=for-the-badge&logo=windows&logoColor=white)

Aplikasi chat desktop modern untuk komunikasi dalam jaringan lokal

</div>

---

## 📖 Deskripsi

**DisForms** adalah aplikasi chat berbasis desktop yang memungkinkan komunikasi real-time antar pengguna dalam jaringan lokal (LAN). Dibangun dengan arsitektur client-server menggunakan C# WPF, aplikasi ini menyediakan interface modern dan responsif untuk komunikasi dalam jaringan lokal.

---

## ✨ Fitur

- 💬 Chat real-time dalam jaringan lokal
- 🏗️ Arsitektur client-server
- 🎨 Interface WPF yang modern dan user-friendly
- 🔄 Multi-client support
- 📡 Komunikasi menggunakan TCP/IP Socket

---

## 🏗️ Struktur Proyek

```
DisForms/
│
├── 📁 DisClient/                    # Aplikasi Client
│   ├── App.xaml                     # Application resource
│   ├── App.xaml.cs                  # Application logic
│   ├── Client.cs                    # Client networking logic
│   ├── ClientLobby.xaml             # Lobby window
│   ├── ClientLobby.xaml.cs          # Lobby logic
│   ├── MainWindow.xaml              # Main chat window
│   ├── MainWindow.xaml.cs           # Chat logic
│   └── DisClient.csproj             # Project file
│
├── 📁 DisServer/                    # Aplikasi Server
│   ├── bin/                         # Binary output
│   ├── obj/                         # Object files
│   ├── ClientHandler.cs             # Handle client connections
│   ├── Program.cs                   # Server entry point
│   ├── Server.cs                    # Server networking logic
│   └── DisServer.csproj             # Project file
│
└── 📄 DisForms.sln                  # Visual Studio Solution
```

---

## 🛠️ Teknologi

- **Bahasa**: C#
- **Framework**: .NET WPF (Windows Presentation Foundation)
- **Networking**: TCP/IP Socket Programming
- **IDE**: JetBrains Rider / Visual Studio

---

## 📋 Prasyarat

- Windows 10 atau lebih baru
- .NET 6.0 atau lebih tinggi
- JetBrains Rider / Visual Studio 2022

---

## 🚀 Cara Menjalankan

### 1️⃣ Clone Repository

```bash
git clone https://github.com/Sandy4j/DisForms.git
cd DisForms
```

### 2️⃣ Buka Project

Buka file `DisForms.sln` dengan JetBrains Rider atau Visual Studio

### 3️⃣ Jalankan Server

- Set **DisServer** sebagai startup project
- Build dan jalankan aplikasi server
- Server akan mulai listening pada port yang dikonfigurasi

### 4️⃣ Jalankan Client

- Set **DisClient** sebagai startup project
- Build dan jalankan aplikasi client
- Masukkan server address dan connect

---

## 🎓 Informasi Akademik

**Mata Kuliah:** Network Programming  
**Institusi:** Politeknik Elektronika Negeri Surabaya

---

## 👥 Tim Pengembang

<table>
  <tr>
    <td align="center">
      <a href="https://github.com/Sandy4j">
        <img src="https://github.com/Sandy4j.png" width="100px;" alt="Yoga Sandy"/><br />
        <sub><b>Yoga Sandy</b></sub>
      </a>
    </td>
    <td align="center">
      <a href="https://github.com/XTripsy">
        <img src="https://github.com/XTripsy.png" width="100px;" alt="Aditya Muhammad Ifanrus"/><br />
        <sub><b>Aditya Muhammad Ifanrus</b></sub>
      </a>
    </td>
    <td align="center">
      <a href="https://github.com/BintangBimantara">
        <img src="https://github.com/BintangBimantara.png" width="100px;" alt="Bintang Bimantara"/><br />
        <sub><b>Bintang Bimantara</b></sub>
      </a>
    </td>
  </tr>
</table>
---

## 📝 Lisensi

Proyek ini dibuat untuk keperluan pendidikan.

---

<div align="center">

Made with ❤️ by DisForms Team

</div>
