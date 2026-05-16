# 🚀 Otimizador de Sistema (System Optimizer)

![Platform](https://img.shields.io/badge/platform-Windows%20%7C%2011-blue)
![Language](https://img.shields.io/badge/language-C%23%2014%20%7C%20.NET%2010-purple)
![License](https://img.shields.io/badge/license-MIT-green)
[![Novidades](https://img.shields.io/badge/Changelog-Ver%20Histórico%20de%20Versão-blueviolet)](https://github.com/JohnWiliam/Otimizador-Windows/blob/main/CHANGELOG.md)
[![GitHub downloads](https://img.shields.io/github/downloads/JohnWiliam/Otimizador-Windows/total?color=green&logo=github)](https://github.com/JohnWiliam/Otimizador-Windows/releases/)

> Criado e Idealizado por John Wiliam com o auxílio de IA.

* **Idioma/Language**
[🇧🇷 Português](#-português) | [🇺🇸 English](#-english)

---
# 🚀 Otimizador de Sistema (System Optimizer)

## 🇧🇷 Português

O **Otimizador de Sistema** é uma aplicação moderna, portátil e robusta desenvolvida em **C# 14 (WPF)** para ajustar, limpar e acelerar o Windows. Ele utiliza a biblioteca **WPF-UI 4.1** para oferecer uma interface elegante com efeitos **Mica/Acrylic** e **Fluent Design**, garantindo uma experiência nativa no Windows 11.

Nesta versão, o aplicativo conta com controles granulares (botões individuais de "Aplicar" e "Restaurar") e um sistema de **Auto-Update** integrado.

### ✨ Funcionalidades Principais

O aplicativo é dividido em categorias inteligentes para facilitar o uso:

#### 🔄 Atualizações (Updates)
Mantenha o sistema sempre na última versão sem esforço.
* **Verificação Integrada**: Consulta a API do GitHub Releases para encontrar novidades.
* **Instalação Silenciosa**: O sistema baixa e substitui o executável automaticamente em segundo plano.
* **Interface de Progresso**: Visualize as notas da versão e o progresso do download em tempo real.

#### 🛡️ Privacidade (Privacy)
Proteja seus dados desativando serviços invasivos do Windows.
* 🚫 **Telemetria**: Impede o envio de dados de diagnóstico.
* 🕵️ **Rastreamento**: Desativa DiagTrack e IDs de publicidade.
* 🤖 **Cortana**: Bloqueia o assistente legado.
* 📍 **Geolocalização**: Restringe o rastreamento global de posição.

#### ⚡ Performance
Extraia o máximo do seu hardware.
* 🔋 **Plano Ultimate**: Ativa o plano de energia de desempenho máximo oculto.
* 🎮 **GameDVR**: Desativa gravações em segundo plano para aumentar FPS.
* 🖱️ **Mouse 1:1**: Remove a aceleração do mouse para precisão em jogos.
* 🚀 **VBS / HVCI**: Desativa o isolamento de núcleo (pode aumentar desempenho em jogos).

#### 🌐 Rede (Network)
Otimize sua conexão para menor latência e maior estabilidade.
* 📶 **TCP Auto-Tuning**: Ajuste dinâmico da janela TCP.
* 📦 **Algoritmo CUBIC**: Gestão moderna de congestionamento para alta velocidade.
* 🔔 **ECN & RSS**: Notificação de congestionamento e escalonamento de recepção, preservando RSS em hardware moderno com múltiplos núcleos.

#### 🔒 Segurança (Security)
Fortaleça a proteção básica do sistema.
* 📄 **Extensões de Arquivo**: Exibe as extensões reais (.exe, .bat) para evitar malwares disfarçados.
* 🚫 **AutoRun**: Bloqueia a execução automática de dispositivos USB.

#### 🔎 Pesquisa (Search)
Remova integrações web e limpe o Menu Iniciar.
* ⌨️ **Sugestões de Pesquisa**: Desativa sugestões online e histórico recente ao digitar.
* ☁️ **Busca na Nuvem**: Impede que o Windows pesquise conteúdo nas suas contas (OneDrive, Outlook).
* 🌎 **Bing**: Remove resultados da web, notícias e anúncios do Menu Iniciar.
* 🔄 **Reiniciar Explorer**: Botão dedicado para aplicar as alterações instantaneamente.

#### 🎨 Visual (Appearance)
Ajuste a aparência para focar em desempenho ou estética.
* 🌑 **Modo Escuro**: Força o tema escuro em aplicativos compatíveis.
* 🪟 **Transparência**: Desativa efeitos de transparência para economizar recursos (útil em PCs modestos).
* ✨ **Efeitos Visuais**: Ajusta as configurações do sistema para "Melhor Desempenho" (parcial).

#### 🛠️ Ajustes Finos (Tweaks)
Funcionalidades avançadas com foco na longevidade do hardware (SSDs) e automação.
* 🧠 **SysMain (Superfetch)**: Otimiza o serviço de pré-busca, reduzindo uso de disco/RAM (Ideal para SSDs).
* 🛑 **Prefetch**: Só desativa a pré-busca quando a unidade de sistema é detectada como SSD; em HDD ela permanece ativa para evitar perda de desempenho.
* 🤖 **Persistência Inteligente**: Cria uma tarefa agendada para reaplicar otimizações silenciosamente a cada login, impedindo que o Windows as reverta.

#### 🧹 Limpeza Inteligente (Cleanup)
Uma ferramenta poderosa para liberar espaço.
* 🗑️ **Arquivos Temporários**: Limpa `Temp` do Usuário e Sistema.
* 🚀 **Prefetch & Shader Cache**: Remove caches antigos (DX e D3D).
* 🔄 **Windows Update (Smart)**: Para os serviços (`wuauserv`, `bits`), limpa os arquivos baixados e reinicia os serviços com segurança.
* 🌐 **Cache de Navegadores**: Limpa cache do Chrome.
* 🐛 **Logs e CrashDumps**: Remove relatórios de erro acumulados.

### 🏗️ Estrutura do Projeto

O projeto segue a arquitetura **MVVM (Model-View-ViewModel)** com **Injeção de Dependência**, garantindo código limpo e testável.

* `src/SystemOptimizer/`
    * 📂 **Assets/**: Ícones e imagens de alta resolução.
    * 📂 **Models/**: Definições de Tweaks (`RegistryTweak`, `CustomTweak`).
    * 📂 **Services/**: Lógica de negócio (`TweakService`, `CleanupService`, `DialogService`, `UpdateService`).
    * 📂 **ViewModels/**: Lógica de apresentação (`MainViewModel`).
    * 📂 **Views/**: Interfaces XAML (`MainWindow`, `Pages/`).
* 📜 **build.ps1**: Script automatizado para compilar o executável portátil.

### 🚀 Como Compilar

Você precisa do **.NET 10 SDK** instalado.

1.  Abra o terminal na pasta raiz do projeto.
2.  Execute o script de build:
    ```powershell
    .\build.ps1
    ```
3.  O executável final estará em: `Build\SystemOptimizer.exe`.
    * *Nota: O arquivo é "Self-Contained" (não requer instalação do .NET no PC alvo) e comprimido.*

### ⚠️ Aviso
Este software modifica configurações do registro e serviços do sistema. Embora tenha sido testado e inclua a função **"Restaurar Seleção"**, use por sua conta e risco. Execute sempre como **Administrador**.

---

## 🇺🇸 English

**System Optimizer** is a modern, portable, and robust application built in **C# 14 (WPF)** to tweak, clean, and accelerate Windows. It leverages the **WPF-UI 4.1** library to deliver a sleek interface with **Mica/Acrylic** effects and **Fluent Design**, ensuring a native feel on Windows 11.

This version features granular controls (individual "Apply" and "Restore" buttons) and an integrated **Auto-Update** system.

### ✨ Key Features

The application is organized into smart categories for ease of use:

#### 🔄 Updates
Keep the system always up to date effortlessly.
* **Automatic Check**: Queries GitHub Releases API for updates.
* **Silent Install**: Downloads and replaces the executable automatically in the background.
* **Progress UI**: View release notes and download progress in real-time.

#### 🛡️ Privacy
Protect your data by disabling invasive Windows services.
* 🚫 **Telemetry**: Prevents sending diagnostic data.
* 🕵️ **Tracking**: Disables DiagTrack and Advertising IDs.
* 🤖 **Cortana**: Blocks the legacy assistant.
* 📍 **Geolocation**: Restricts global location tracking.

#### ⚡ Performance
Squeeze the most out of your hardware.
* 🔋 **Ultimate Plan**: Activates the hidden maximum performance power plan.
* 🎮 **GameDVR**: Disables background recording to boost FPS.
* 🖱️ **Mouse 1:1**: Removes mouse acceleration for gaming precision.
* 🚀 **VBS / HVCI**: Disables core isolation (can improve gaming performance).

#### 🌐 Network
Optimize your connection for lower latency and better stability.
* 📶 **TCP Auto-Tuning**: Dynamic adjustment of the TCP window.
* 📦 **CUBIC Algorithm**: Modern congestion management for high speeds.
* 🔔 **ECN & RSS**: Explicit Congestion Notification and Receive Side Scaling.

#### 🔒 Security
Strengthen basic system protection.
* 📄 **File Extensions**: Displays real file extensions (.exe, .bat) to avoid disguised malware.
* 🚫 **AutoRun**: Blocks automatic execution of USB devices.

#### 🔎 Search
Remove web integrations and clean up the Start Menu.
* ⌨️ **Search Suggestions**: Disables online suggestions and recent history when typing.
* ☁️ **Cloud Search**: Prevents Windows from searching content in your cloud accounts (OneDrive, Outlook).
* 🌎 **Bing**: Removes web results, news, and ads from the Start Menu.
* 🔄 **Restart Explorer**: Dedicated button to apply changes instantly.

#### 🎨 Appearance
Adjust the look and feel to focus on performance or aesthetics.
* 🌑 **Dark Mode**: Forces dark theme on compatible apps.
* 🪟 **Transparency**: Disables transparency effects to save resources (useful for low-end PCs).
* ✨ **Visual Effects**: Adjusts system settings for "Best Performance" (partial).

#### 🛠️ Tweaks
Advanced features focused on hardware longevity (SSDs) and automation.
* 🧠 **SysMain (Superfetch)**: Optimizes prefetch service reducing disk/RAM usage (Ideal for SSDs).
* 🛑 **Prefetch**: Disables boot tracking files to save write cycles.
* 🤖 **Smart Persistence**: Creates a scheduled task to silently reapply optimizations on every login, preventing Windows from reverting them.

#### 🧹 Smart Cleanup
A powerful tool to free up space.
* 🗑️ **Temporary Files**: Cleans User and System `Temp`.
* 🚀 **Prefetch & Shader Cache**: Removes old caches (DX and D3D).
* 🔄 **Windows Update (Smart)**: Safely stops services (`wuauserv`, `bits`), cleans downloaded files, and restarts services.
* 🌐 **Browser Cache**: Cleans Chrome cache.
* 🐛 **Logs & CrashDumps**: Removes accumulated error reports.

### 🏗️ Project Structure

The project follows the **MVVM (Model-View-ViewModel)** architecture with **Dependency Injection**, ensuring clean and testable code.

* `src/SystemOptimizer/`
    * 📂 **Assets/**: High-resolution icons and images.
    * 📂 **Models/**: Tweak definitions (`RegistryTweak`, `CustomTweak`).
    * 📂 **Services/**: Business logic (`TweakService`, `CleanupService`, `DialogService`, `UpdateService`).
    * 📂 **ViewModels/**: Presentation logic (`MainViewModel`).
    * 📂 **Views/**: XAML Interfaces (`MainWindow`, `Pages/`).
* 📜 **build.ps1**: Automated script to compile the portable executable.

### 🚀 How to Build

You need the **.NET 10 SDK** installed.

1.  Open a terminal in the project root folder.
2.  Run the build script:
    ```powershell
    .\build.ps1
    ```
3.  The final executable will be located at: `Build\SystemOptimizer.exe`.
    * *Note: The file is "Self-Contained" (does not require .NET installed on the target PC) and compressed.*

### ⚠️ Disclaimer
This software modifies system registry settings and services. While it has been tested and includes a **"Restore Selection"** feature, use at your own risk. Always run as **Administrator**.
