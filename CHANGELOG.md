[![GitHub downloads](https://img.shields.io/github/downloads/JohnWiliam/Otimizador-Windows/total?color=green&logo=github)](https://github.com/JohnWiliam/Otimizador-Windows/releases/)

**🚀 Otimizador de Sistema v1.2.0**

Esta atualização foca na experiência visual e integração com o sistema, trazendo o tão aguardado suporte nativo ao Modo Escuro.

**✨ O Que Há de Novo?**
🌗 Suporte Automático a Temas (Dark/Light Mode)

A aplicação agora sincroniza-se automaticamente com a aparência do seu Windows.

* **Detecção Inteligente:** O aplicativo identifica se o Windows está configurado para o tema "Claro" ou "Escuro" e adapta a interface instantaneamente.
* **Experiência Nativa:** Utilização do `SystemThemeWatcher` para garantir que a troca de temas ocorra de forma fluida e integrada ao sistema operacional.

**🎨 Refatoração Visual e Correções**

Ajustes profundos no código XAML para garantir legibilidade e contraste perfeitos.

* **Cores Dinâmicas:** Substituição de cores fixas por recursos dinâmicos (`DynamicResource`). Isso corrige problemas onde textos ficavam invisíveis ou com baixo contraste em fundos escuros.
* **Correção de Interface:** Títulos e descrições agora utilizam os pinceis de sistema corretos (`TextFillColorPrimaryBrush`), garantindo que a interface permaneça moderna e legível em qualquer cenário de iluminação.

---

**🚀 Otimizador de Sistema v1.1.1**

Esta atualização traz um refinamento visual significativo para as ferramentas de limpeza e consolida a gestão de ajustes do sistema.

**✨ O Que Há de Novo?**
🧹 Página de Limpeza Aprimorada (Visual Log 2.0)

A interface de limpeza foi reescrita para fornecer feedback visual instantâneo e detalhado (baseado no código em `CleanupPage.xaml.cs`).

* **Sistema de Cores Inteligente (Smart Pastel):** O log de execução agora utiliza uma codificação de cores intuitiva para facilitar a leitura:
    * 🟢 **Verde Pastel:** Atualizações do Windows e Serviços.
    * 🟠 **Pêssego:** Remoção de arquivos temporários, lixeira e cache.
    * 🟡 **Creme:** Limpeza de navegadores e histórico de internet.
    * 🟣 **Lavanda:** Ajustes de rede e sistema.
    * 🔴 **Salmão:** Erros ou falhas de permissão.
* **Ícones Dinâmicos:** Cada linha de log agora é acompanhada por um ícone contextual (`SymbolIcon`) que representa o tipo de ação (vassoura, check, alerta), tornando o acompanhamento do processo muito mais visual.
* **RichTextBox Integration:** Migração para um componente de texto rico que permite formatação avançada e melhor legibilidade.

**🛠️ Criação da Aba Tweaks**

Implementação da interface gráfica dedicada para a aplicação de ajustes (Tweaks), conforme visualizado em `TweaksPage.xaml`.

* **Interface de Seleção:** Nova UI baseada em `Cards` que permite selecionar múltiplos tweaks via checkboxes.
* **Ações em Lote:** Botões dedicados para "Aplicar Seleção" e "Restaurar Seleção", facilitando o gerenciamento em massa das otimizações.
* **Feedback de Status:** Visualização clara do estado atual e descrição detalhada de cada ajuste disponível no sistema.

---

**🚀 Otimizador de Sistema v1.1.0**

Apresentamos a versão 1.1.0, trazendo uma nova categoria poderosa e funcionalidades focadas na longevidade do teu hardware.
**✨ O Que Há de Novo?**
**🛠️ Nova Aba:** Tweaks

Adicionámos uma secção dedicada a ajustes finos do sistema, migrando funcionalidades avançadas de scripts PowerShell diretamente para a interface nativa em C#.

    Foco Total em SSDs: Estas novas opções foram desenhadas para reduzir a escrita desnecessária em disco e libertar recursos em segundo plano.

**🔥 Funcionalidades em Destaque**
**1. ⚡ Desativar SysMain (Superfetch)**

Otimiza a performance para quem utiliza SSDs.

    O que faz: Desativa o serviço que pré-carrega aplicações na memória RAM.

    Benefício: Reduz o uso constante do disco e liberta RAM. Em SSDs modernos, o pré-carregamento é muitas vezes desnecessário e consome ciclos de vida útil do disco.

**2. 🛑 Desativar Prefetch**

Para um sistema mais limpo e com menos "lixo" de rastreamento.

    O que faz: Impede que o Windows crie ficheiros de rastreamento de inicialização em C:\Windows\Prefetch.

    Benefício: Menos operações de escrita (Write Operations), o que é vital para a saúde a longo prazo do teu SSD.

**3. 🤖 Persistência Inteligente (Silent Mode)**

A funcionalidade mais robusta desta atualização.

    O que faz: Cria uma tarefa agendada que executa o Otimizador em Modo Silencioso (--silent) a cada login.

    Por que é importante: O Windows Update tende a reverter as tuas otimizações. Com a persistência ativada, o programa garante que os teus Tweaks favoritos são reaplicados automaticamente a cada reinício, sem abrir janelas ou interromper o teu fluxo.

**⚙️ Melhorias Técnicas**

    Refatoração de Código: Migração de lógica .ps1 para C# nativo.

    Argumento --silent: O executável agora suporta execução sem interface gráfica para automação.

    Cleanup: A opção SysMain foi movida da aba Performance para a nova aba Tweaks para melhor organização.

Desenvolvido por John Wiliam & IA 💻
