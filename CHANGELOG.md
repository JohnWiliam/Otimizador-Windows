🚀 Otimizador de Sistema v1.1.0

Apresentamos a versão 1.1.0, trazendo uma nova categoria poderosa e funcionalidades focadas na longevidade do teu hardware.
✨ O Que Há de Novo?
🛠️ Nova Aba: Tweaks

Adicionámos uma secção dedicada a ajustes finos do sistema, migrando funcionalidades avançadas de scripts PowerShell diretamente para a interface nativa em C#.

    Foco Total em SSDs: Estas novas opções foram desenhadas para reduzir a escrita desnecessária em disco e libertar recursos em segundo plano.

🔥 Funcionalidades em Destaque
1. ⚡ Desativar SysMain (Superfetch)

Otimiza a performance para quem utiliza SSDs.

    O que faz: Desativa o serviço que pré-carrega aplicações na memória RAM.

    Benefício: Reduz o uso constante do disco e liberta RAM. Em SSDs modernos, o pré-carregamento é muitas vezes desnecessário e consome ciclos de vida útil do disco.

2. 🛑 Desativar Prefetch

Para um sistema mais limpo e com menos "lixo" de rastreamento.

    O que faz: Impede que o Windows crie ficheiros de rastreamento de inicialização em C:\Windows\Prefetch.

    Benefício: Menos operações de escrita (Write Operations), o que é vital para a saúde a longo prazo do teu SSD.

3. 🤖 Persistência Inteligente (Silent Mode)

A funcionalidade mais robusta desta atualização.

    O que faz: Cria uma tarefa agendada que executa o Otimizador em Modo Silencioso (--silent) a cada login.

    Por que é importante: O Windows Update tende a reverter as tuas otimizações. Com a persistência ativada, o programa garante que os teus Tweaks favoritos são reaplicados automaticamente a cada reinício, sem abrir janelas ou interromper o teu fluxo.

⚙️ Melhorias Técnicas

    Refatoração de Código: Migração de lógica .ps1 para C# nativo.

    Argumento --silent: O executável agora suporta execução sem interface gráfica para automação.

    Cleanup: A opção SysMain foi movida da aba Performance para a nova aba Tweaks para melhor organização.

Desenvolvido por John Wiliam & IA 💻
