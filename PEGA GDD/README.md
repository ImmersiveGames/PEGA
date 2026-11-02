# PEGA — índice da documentação

## Fontes de autoridade

- [GDD](GDD.md) — regras e comportamento do jogo.
- [MVP](MVP.md) — escopo e critérios do vertical slice e da Demo.
- [ADR de pré-produção](ADR-PREPRODUCAO.md) — decisões de arquitetura transversal e produção.
- [ADRs](ADRs/) — decisões técnicas específicas, subordinadas ao GDD e ao MVP.
- [TASKS](TASKS.md) — decomposição executável derivada das fontes acima.
- [Art Book](ArtBook.md) — direção visual e referências de arte.

## Material complementar

- [Análise macro de objetivos](Analises/pega-gdd-activity-analysis.md) — síntese derivada do GDD/MVP; não é roadmap.
- [Especificações comportamentais](Analises/pega-objective-specifications.md) — contratos derivados para revisão; não substituem GDD, MVP ou ADR.
- [Guia prático de GOAP](Guias/PEGA-GOAP-guia-pratico.md) — superfície experimental de autoria no Unity.
- [Nota de implementação de Perception](Trabalho/notas/PEGA-Perception-implementacao-2026-10-26.md) — registro datado do caminho técnico observado.
- [Registro conceitual histórico do GOAP](ADRs/Historico/PEGA-GOAP-registro-de-decisoes-conceituais-2026-10-25.md) — origem dos acordos depois formalizados nos ADRs.

## Registros de trabalho

Os documentos em [Trabalho](Trabalho/) são snapshots de desenhos, planos e relatórios com datas e escopos próprios. Eles não determinam o estado atual de uma tarefa nem substituem as fontes normativas ou o Trello. Leia [as orientações do arquivo](Trabalho/README.md) antes de usá-los.

## Site da documentação

`index.html` é gerado a partir de `GDD.md` e `ArtBook.md`. Edite os arquivos Markdown e regenere a página com `python tools/build_gdd_site.py` executado dentro desta pasta.
