# ADR-POI-01 — Identidade, manifestação e conhecimento de POIs

- **Status:** Aceito quanto às decisões consolidadas; política de descoberta da localização ainda não decidida
- **Data:** 2026-10-30
- **Owner:** PEGA — mundo, identidade de POIs e integração com conhecimento/GOAP

## Contexto

O GDD descreve POIs como objetos relevantes do cenário e distingue o **World State real** dos fatos legitimamente disponíveis ao **Knowledge State**. Conhecer uma entidade não significa conhecer todas as suas propriedades; o GOAP parte de Targets conhecidos e não consulta estado oculto para completar planos.

Na evolução da demonstração de navegação até um POI, decisões aprovadas em discussão de arquitetura ficaram dispersas entre a conversa e a implementação. Isso permitiu tratar a referência ao objeto como uma combinação obrigatória de identificador textual e coordenadas, ou suprir a falta de uma capacidade com um vetor manual apenas para concluir a demonstração. Este ADR **consolida as decisões já tomadas** e identifica o que ainda requer decisão; não presume que a implementação atual esteja conforme ou validada no Unity.

## Decisões consolidadas

1. **POI é um objeto individual do mundo físico**, não uma oportunidade, evento ou tipo abstrato de comportamento. Pode ser conhecido e referenciado antes de haver uma manifestação física carregada.
2. **A identidade persistente de um POI é um asset ScriptableObject individual**, não um perfil reutilizável. Referenciar o mesmo asset significa referir-se ao mesmo POI, mesmo se sua manifestação se mover, for descarregada ou recriada. Outro POI exige outro asset de identidade. Um identificador técnico estável pode auxiliar persistência ou resolução, mas **não substitui a referência ao asset como identidade de autoria** nem exige digitação de ID textual pelo designer.
3. **Manifestação física é distinta da identidade.** Um GameObject materializado referencia explicitamente o asset de identidade e pertence a um contexto de mundo. A posição e o Transform atuais pertencem à manifestação, não à identidade persistente. Ausência de manifestação não elimina a identidade nem o fato de que alguém conhece o POI.
4. **Unicidade por contexto:** somente uma manifestação física pode ocupar validamente a mesma identidade num contexto de mundo. Uma concorrente não substitui silenciosamente a manifestação registrada. Contextos distintos podem manter resoluções independentes. A política exata de espera/promoção de candidatas é detalhe da implementação, não parte necessária desta decisão.
5. **Resolução física e conhecimento têm autoridades diferentes.** O contexto de POIs registra e resolve manifestações por identidade; não decide o que um NPC ou uma Gangue sabe. O Knowledge State conserva fatos legitimamente adquiridos, suas fontes, validade e políticas. O POI não precisa ser um Actor; os padrões de identidade/ocorrência do Immersive Framework são referência conceitual, não uma imposição de acoplamento.
6. **Conhecer o POI não equivale a conhecer onde ele está.** Um conhecimento Individual ou de Gangue pode referenciar diretamente o asset de identidade sem coordenadas. A existência/identidade e a localização são fatos independentes da mesma entidade; a localização pode ser desconhecida, conhecida ou desatualizada, e uma atualização de posição não troca a identidade.
7. **Conhecimento prévio é legítimo em mais de um escopo.** A autoria Individual já aceita deve ser preservada; conhecimento prévio da Gangue é outra capacidade, mantida na autoridade da Gangue e disponibilizada aos membros autorizados segundo as políticas do GDD. Compartilhar um fato não compartilha Desejos nem determina que todos os membros executem a mesma Action. O POI utilizado na demonstração é um exemplo, não o limite do mecanismo geral de conhecimento.
8. **Planejamento e execução preservam a fronteira com o mundo real.** O GOAP não pode transformar a simples referência ao asset em conhecimento de coordenadas atuais por consulta oculta ao contexto de POIs. O contexto pode resolver a manifestação para operações físicas legítimas e validação de execução, sem que isso conceda automaticamente uma nova observação ao Knowledge State. Quando uma Action exige uma rota até uma posição, sua avaliação precisa de informação de localização legitimamente disponível ou de outro caminho informativo autorizado pelas regras de gameplay. Não se deve inserir um `Vector3` artificial apenas para fazer a demonstração passar.
9. **O exemplo não estreita o contrato.** A integração de POIs deve evoluir as autoridades de identidade e conhecimento existentes por incrementos, sem criar uma autoridade paralela de conhecimento para um NPC, Action ou POI de demonstração. Especializações de Actions e de fontes são legítimas quando consomem os contratos gerais sem contorná-los.

## Questão ainda não decidida

**Como um agente que conhece apenas a identidade de um POI pode descobrir sua localização física atual?**

O GDD autoriza fontes legítimas de informação, como visão, investigação, resultados de Actions e compartilhamento, mas não determina que **conhecer o asset** conceda acesso irrestrito à posição da manifestação no World State. Também não foi encontrada nas decisões consolidadas autorização para tratar a resolução técnica pelo POI Context como evidência perceptiva do NPC.

A escolha da fonte e da política de acesso à localização — inclusive se alguma classe de POI admite localização estrutural pré-conhecida — deve ser examinada contra o GDD e decidida explicitamente **antes** de uma implementação que dependa disso. Até lá, a resolução técnica por identidade, sozinha, não constitui aquisição de conhecimento. Não estabelecer um produtor de percepção ou um comportamento de investigação específico neste ADR.

Também permanecem detalhes de implementação a determinar conforme necessidade: fronteira concreta do ciclo de vida do contexto de mundo, representação técnica/serialização de identificadores auxiliares e política de atualização da navegação após novas evidências de posição. Nenhum deles autoriza misturar identidade, manifestação e conhecimento.

## Consequências e avaliação de incrementos

- O Inspector deve poder expressar **“Gangue conhece: [asset de identidade do POI]”** sem exigir ID digitado ou vetor na mesma entrada.
- Deve ser possível distinguir **POI conhecido sem posição conhecida** de **POI com localização conhecida**; o segundo fato tem proveniência e validade próprias.
- Conhecimento da Gangue, conhecimento Individual, resolução de manifestação e seleção do GOAP não devem ser implementados como atalhos uns dos outros.
- A demonstração de um NPC chegando a um POI não prova, por si, que a arquitetura de conhecimento está correta. A revisão deve conferir primeiro as responsabilidades e os fatos utilizados, depois o comportamento observado.
- Este documento registra contratos de arquitetura, não atesta compilação, Play Mode nem conformidade do código já existente.

## Relação com decisões existentes

Este ADR detalha a identidade e a materialização de POIs conforme as decisões aprovadas na discussão de arquitetura, subordinando sua integração às regras gerais de conhecimento e planejamento do GDD. **Não altera** o GDD, os escopos Individual/Gangue/Global nem os ADRs GOAP. Em particular, não resolve por inferência a questão em aberto sobre descoberta da posição atual.

## Autoridade e rastreabilidade

- [GDD — Áreas, POIs e investigação](../GDD.md#áreas-pois-e-investigação)
- [GDD — Conhecimento modular, memória e validade da informação](../GDD.md#conhecimento-modular-memória-e-validade-da-informação)
- [GDD — Conhecimento da gangue e informação global](../GDD.md#conhecimento-da-gangue-e-informação-global)
- [GDD — Contrato de Actions](../GDD.md#contrato-de-actions)
- [ADR-GOAP-01 — Domínio e evolução por composição](ADR-GOAP-01-composicao-e-fronteiras.md)
- [ADR-GOAP-02 — Condições, Effects e planejamento causal](ADR-GOAP-02-modelo-causal-e-planejamento.md)
- [ADR-GOAP-04 — Tentativas informativas e evidências de interação](ADR-GOAP-04-interacoes-informativas.md)
