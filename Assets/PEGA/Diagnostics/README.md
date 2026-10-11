# Diagnóstico FOUND-01

Esta autoria temporária demonstra somente o Game Flow do Framework.

1. Inicie o Play Mode com `ImmersiveFrameworkSettings` apontando para `PEGAApplication`.
2. Confirme no Console a entrada em Mission Activity com a fase `Preparation` (Preparação).
3. Na cena de conteúdo de Mission Activity carregada, selecione `FOUND-01 Diagnostic` e altere `Phase` de `Preparation` para `Assault` e depois `Escape`. Cada mudança no Inspector durante Play Mode é registrada no Console. São rótulos diagnósticos para Preparação, Assalto e Fuga; não há timers nem comportamento de gameplay.
4. Selecione `FOUND-01 Result Request` na cena da Mission Route, abra `Advanced / Debug` no `ActivityRequestTrigger` e clique `Request Activity`.
5. Confirme o resultado da solicitação, a entrada do conteúdo de Result Activity e a liberação do conteúdo de Mission Activity nos diagnósticos do Framework/Hierarchy.

O Framework mantém a autoridade de Route/Activity e o lifecycle das cenas. O diagnóstico não implementa roteamento, timers, regras de gameplay ou reset.
