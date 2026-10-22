# Custom Difficulty

Mod de dificuldade personalizada para **Hollow Knight: Silksong**.

- **Versão:** `3.0.0`
- **Criador:** `Ericky1694`
- **Menu do mod:** `F8`
- **Dependência:** BepInEx 5 para Silksong

O Custom Difficulty permite alterar a dificuldade da campanha em tempo real, configurar individualmente a Hornet, inimigos e Bosses, usar mutadores especiais e salvar combinações completas em arquivos de preset.

## Principais recursos

- Configurações aplicadas em tempo real.
- Cada opção pode ser ligada ou desligada individualmente.
- Valores podem ser alterados com os botões `−` e `+` ou digitados diretamente.
- Interface responsiva em uma ou duas colunas.
- Resumo visual da dificuldade, pontuação e estilo do perfil.
- Sistema de Favoritos com acesso rápido às opções escolhidas.
- Presets internos e suporte a quantidade ilimitada de presets em arquivo.
- Somente um preset pode permanecer ativo por vez.
- Mutadores com classificação visual de dificuldade.
- Idioma automático baseado no idioma usado pelo jogo.
- Restauração global com confirmação e contador de alterações.
- Configurações, favoritos, idioma e estado dos presets salvos em JSON.

## Controles da interface

| Controle | Ação |
| --- | --- |
| `F8` | Abrir ou fechar o menu |
| `Esc` | Fechar o menu ou cancelar uma confirmação |
| `Q` / `E` | Navegar entre as categorias |
| `Page Up` / `Page Down` | Navegação alternativa entre categorias |
| `Ctrl + F` | Focar a busca de categorias |
| `F` | Adicionar ou remover dos Favoritos a opção sob o cursor |
| `Enter` | Confirmar um valor digitado |

Os campos também são validados quando perdem o foco, ao trocar de categoria ou ao fechar o menu.

## Categorias

### Resumo

Dashboard do perfil atual com:

- Classificação da dificuldade.
- Pontuação estimada.
- Estilo predominante.
- Quantidade de mutadores ativos.
- Métricas de sobrevivência, dano, mobilidade, inimigos, Bosses e cura.

### Favoritos

Mostra atalhos para todas as configurações marcadas com estrela. Os cartões mantêm os mesmos controles e indicam a categoria de origem. Favoritos são preservados ao restaurar as configurações e podem fazer parte dos presets salvos pelo usuário.

### Jogador

- **Vida máxima:** de 1 a 20 máscaras.
- **Dano recebido:** de 1 a 20.
- **Dano de cenário:** de 1 a 20.
- **Modo One Hit:** qualquer dano recebido remove toda a vida normal e azul.
- **Quantidade de cura:** de 1 a 20 máscaras por Bind.
- **Custo da cura:** de 1 a 18 unidades de Seda.
- **Tempo de invencibilidade:** de `0,1x` a `20,0x`.

### Dano e Ferramentas

- **Dano base da Agulha:** de 1 a 100.
- **Aumento por melhoria:** de 0% a 100% por melhoria obtida.
- Custos de Seda individuais, de 1 a 18, para as seis Habilidades de Seda.
- Multiplicadores de dano individuais das Habilidades de Seda.
- Configuração específica da Lança de Seda, cujo dano padrão é `3x` e o custo padrão é 4.
- Catálogo visual de Ferramentas Brancas, Vermelhas, Azuis e Amarelas com seus respectivos ícones.

Habilidades de Seda disponíveis:

1. Lança de Seda.
2. Turbilhão de Fios.
3. Ponto Cruz.
4. Dardo Afiado.
5. Fúria de Runas.
6. Ferrões Pálidos.

### Inimigos

- **Vida dos inimigos comuns:** de `0,1x` a `20,0x`.
- **Velocidade dos inimigos comuns:** de `0,1x` a `1,1x`.
- **Recarga de ataque:** de `0,1x` a `20,0x`.
- **Agressividade e alcance de detecção:** de `0,1x` a `20,0x`.

Bosses identificados não recebem as configurações da categoria Inimigos; eles usam a categoria própria.

### Bosses

- **Vida dos Bosses:** de `0,1x` a `20,0x`.
- **Dano recebido de Boss:** de `0,1x` a `20,0x`.
- **Velocidade dos Bosses:** de `0,1x` a `1,1x`.
- **Recarga de ataque dos Bosses:** de `0,1x` a `2,0x`.
- **Modo Boss Brutal:** cada ataque de Boss tem 10% de chance de causar 1 máscara adicional e a cura restaura 1 máscara a menos durante a luta.
- **Sem Cura em Boss:** o Bind pode consumir Seda, mas não restaura máscaras durante uma luta ativa.
- **Cura durante Boss:** multiplica somente a quantidade recuperada durante uma luta de Boss.

### Editor de inimigos

Área reservada para a edição individual de inimigos e Bosses catalogados. As opções exibidas nesta área permanecem bloqueadas enquanto a implementação individual não estiver finalizada.

### Recompensas

- Multiplicador de Rosários recebidos por moedas coletadas: de `0,1x` a `20,0x`.
- Multiplicador de recompensas recebidas por fragmentos coletados: de `0,1x` a `20,0x`.

### Economia

- Preços de lojas: de `0,1x` a `10,0x`.
- Preços de bancos e estações Bellway: de `0,1x` a `10,0x`.
- Perda de Rosários na morte: de 0% a 200%.

Valores acima de 100% podem deixar o saldo de Rosários negativo. Enquanto o saldo for menor que o preço exigido, nenhuma compra pode ser concluída. Ao recuperar o casulo, ele devolve no máximo o saldo positivo existente antes da morte.

### Movimento

- Velocidade de caminhada e corrida da Hornet: de `0,1x` a `2,0x`.
- Recarga do dash: níveis de 1 a 20.

O nível 1 mantém o comportamento normal. O nível 2 define uma espera de 0,1 segundo e cada nível seguinte dobra esse tempo.

## Mutadores

Os mutadores são regras especiais aplicadas sobre as configurações comuns:

| Mutador | Efeito principal |
| --- | --- |
| Sem Cura | Remove a capacidade de curar durante toda a campanha |
| Gravidade Baixa | Mantém a gravidade da Hornet em `0,5x` |
| Dano Dobrado | Inimigos e cenário causam dano dobrado; o dano da Hornet não muda |
| Canhão de Vidro | Vida máxima 1, dano da Agulha 30 e fragmentos de máscara concedem 1.000 Rosários |
| Inimigos Rápidos | Velocidade dos inimigos, inclusive Bosses, em `1,1x` |
| Inimigos Tanque | Vida de todos os inimigos multiplicada por `3x` |
| Inimigos Agressivos | Dano em `2x` e velocidade em `1,1x` |
| Vida Aleatória dos Inimigos | Sorteia individualmente a vida entre `0,1x` e `5,0x` |
| Mundo Rico | Rosários de moedas multiplicados por `30x` |
| Mundo Pobre | Concede metade dos Rosários de moedas |
| Lojas Caras | Preços de lojas, bancos e estações multiplicados por `10x` |
| Sem Perda ao Morrer | Mantém os Rosários e impede a criação do casulo |
| Fúria dos Bosses | Bosses causam `2x` de dano e se movimentam em `1,1x` |
| Sem Misericórdia | Perde todos os Rosários, não cria casulo e retorna ao início do jogo |
| Lutas Prolongadas | Bosses causam 1 máscara de dano e possuem `10x` mais vida |

## Presets

### Presets incluídos no mod

- Muito Fácil.
- Fácil.
- Normal.
- Difícil.
- Muito Difícil.
- Nightmare.
- Farm.

Cada preset pode ser ligado ou desligado. Quando outro preset é ativado, o preset anterior é desativado automaticamente. Ao desligar o preset ativo, o mod restaura o estado que existia antes da ativação.

### Seus Presets

- **Salvar arquivo:** cria um JSON com todas as configurações atuais de todas as categorias.
- **Carregar arquivo:** adiciona o preset selecionado à lista da interface sem ativá-lo automaticamente.
- **Excluir da aba:** remove o atalho da interface, mas preserva o arquivo JSON na pasta de presets.
- A lista não possui um limite artificial de quantidade.
- Presets antigos compatíveis são convertidos para o formato atual quando carregados.

Arquivos de preset ficam em:

`BepInEx/plugins/CustomDifficulty/Presets`

## Idiomas

Idiomas disponíveis:

- Inglês.
- Francês.
- Alemão.
- Espanhol.
- Italiano.
- Português do Brasil.
- Russo.
- Japonês.
- Coreano.
- Chinês simplificado.
- Chinês tradicional.

No modo **Automático**, a interface acompanha o idioma atual do jogo. Se o idioma detectado não possuir um arquivo correspondente, o mod usa Inglês como alternativa.

Os arquivos de tradução ficam em:

`BepInEx/plugins/CustomDifficulty/Localization`

## Arquivos de configuração

Configurações principais:

`BepInEx/config/CustomDifficulty/settings.json`

Estado da interface de presets:

`BepInEx/config/CustomDifficulty/preset-state.json`

## Instalação manual

1. Instale o BepInEx 5 compatível com Hollow Knight: Silksong.
2. Extraia o pacote `CustomDifficulty-3.0.0.zip`.
3. Copie a pasta `BepInEx` extraída para a pasta principal do jogo.
4. Confirme a existência do arquivo:

   `BepInEx/plugins/CustomDifficulty/CustomDifficulty.dll`

5. Inicie o jogo e pressione `F8` para abrir o menu.

## Compatibilidade

O mod utiliza Harmony e controladores em tempo de execução para alterar:

- Vida e dano da Hornet.
- Cura, custo de Seda e invencibilidade.
- Dano e custo das Habilidades de Seda.
- Vida, movimento, recarga de ataques e percepção dos inimigos.
- Regras específicas de Bosses.
- Recompensas, preços e perda de Rosários.
- Movimento e recarga do dash.
- Regras especiais dos mutadores.

Mods que alterem os mesmos métodos ou dados do jogo podem gerar conflitos. Faça backup dos arquivos de configuração e dos presets antes de combinar modificações extensas.

## Interface e resoluções

A interface foi preparada para:

- `1280 × 720`: uma coluna de cartões.
- `1600 × 900`: duas colunas quando houver espaço suficiente.
- `1920 × 1080`: duas colunas com cartões mais largos.

Textos longos usam reticências e exibem o conteúdo completo em tooltip. A aba Sistema possui uma prévia visual do idioma selecionado.

## Restauração

- O botão de reset individual afeta somente a configuração selecionada.
- O reset de categoria afeta somente a categoria atual.
- **Restaurar Tudo** exige confirmação e mostra a quantidade de alterações que será restaurada.
- Favoritos e arquivos de preset são preservados pela restauração global.

## Versionamento

O projeto utiliza o formato:

`MAIOR.MENOR.CORREÇÃO`

- Primeiro número: grande atualização.
- Segundo número: atualização pequena.
- Terceiro número: correções de bugs.

Todo o desenvolvimento desta grande atualização permanece na versão `3.0.0` até o lançamento.

## Créditos

Criado por **Ericky1694**.
