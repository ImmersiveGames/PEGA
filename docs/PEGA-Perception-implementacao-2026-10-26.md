# PEGA — Implementação da integração de Perception

**Data:** 26 de outubro de 2026
**Natureza:** nota descritiva de implementação local; não é ADR nem amplia decisões arquiteturais.

Esta implementação aplica a fronteira aprovada na [decisão de GOAP e Perception, ADR de pré-produção §7](../PEGA%20GDD/ADR-PREPRODUCAO.md#7-evolução-arquitetural-de-goap-e-perception) e no [ADR-GOAP-01, itens 1–5](../PEGA%20GDD/ADRs/ADR-GOAP-01-composicao-e-fronteiras.md#decisão). Os detalhes abaixo descrevem o caminho atual do PEGA; não definem a composição obrigatória para outros jogos.

## Fronteiras e composição atuais

- O assembly `ImmersiveGames.Perception.Runtime` contém contratos de observação, definições de sensor e a lógica genérica de amostragem. Não depende de PEGA, GOAP ou Immersive Framework.
- `PerceptionSensorDefinition` e suas implementações são assets `ScriptableObject` de configuração sem estado runtime mutável.
- `PerceptionSensorSet` é uma classe C# comum, não um componente Unity. Recebe o Transform observador e uma coleção de definições; cada instância mantém seu próprio buffer de resultados.
- A superfície PEGA é `PEGANpcPerceptionSource`. Ela referencia diretamente os assets `RadialPresenceSensor` e `VisionConeSensor` que consome, cria uma instância de `PerceptionSensorSet` para o Actor e associa cada resultado à referência do asset que o produziu. Não há seletores textuais de sensor no Source.
- O `SensorId` continua no asset como identidade genérica, validação de unicidade e diagnóstico do mecanismo. Não escolhe o papel de gameplay no PEGA.
- O Source interpreta as observações para atualizar presença radial, identidade, posição conhecida e contato visual no Knowledge State; também conserva os gatilhos de reavaliação por perda/readquisição. O mecanismo genérico apenas produz observações.
- O prefab base referencia um único componente Unity de Perception — `PEGANpcPerceptionSource` — e atribui diretamente os dois assets. Os valores preservados são raio radial de **6 m**, alcance de visão de **10 m**, FOV de **100°**, origem de **1,55 m** e máscara de obstrução serializada como **64**.

Um sensor adicional não passa a afetar o comportamento PEGA apenas por existir como asset: a integração do jogo deve consumir explicitamente sua observação. Essa escolha evita uma lista PEGA aparentemente extensível cujos resultados não teriam uso definido.

## Evidência e estado de validação

O código e o prefab demonstram a composição serializada e a implementação presentes. Checagens estáticas confirmaram um componente Source no prefab, ausência do antigo componente `PerceptionSensorSet`, referências aos dois assets e permanência dos caminhos de Knowledge State e reavaliação.

O usuário informou que a configuração funciona sem erros. O escopo dos cenários executados não foi especificado nesta conversa; portanto, esse relato não é registrado como cobertura completa de visão, obstrução, perda/readquisição ou Gizmos.
