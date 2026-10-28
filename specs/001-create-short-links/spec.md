# Feature Specification: Encurtamento de links

**Feature Branch**: Nenhuma (sem hook de criacao de branch)

**Created**: 2026-10-19

**Status**: Draft

**Input**: Criar links curtos para URLs http/https, permitir codigo personalizado e expiracao,
redirecionar visitantes e disponibilizar contagem de acessos persistente.

## Clarifications

### Session 2026-10-19

- Q: A criação pública de links deve ter um limite para evitar uso abusivo? → A: Ate 60 criacoes por hora por endereco IP; redirecionamentos sem limite.
- Q: Qual operação deve exigir a chave de gerenciamento? → A: Consultar contagem; a chave e entregue na criacao e exigida para consultar acessos.

## User Scenarios & Testing _(mandatory)_

### User Story 1 - Criar link curto (Priority: P1)

Como criador, informo uma URL de destino valida e recebo um link curto para compartilhar.

**Why this priority**: Sem a criacao do link nao ha produto utilizavel.

**Independent Test**: Criar um link sem codigo personalizado e verificar que o resultado
contem um codigo de sete caracteres alfanumericos e aponta para a URL informada.

**Acceptance Scenarios**:

1. **Given** uma URL http ou https valida com ate 2048 caracteres, **When** o criador
   solicita o link sem escolher codigo, **Then** recebe um link compartilhavel com codigo
   alfanumerico de exatamente sete caracteres, exclusivo no sistema.
2. **Given** uma URL com esquema diferente de http/https, vazia ou com mais de 2048
   caracteres, **When** o criador tenta criar o link, **Then** a criacao e recusada com
   mensagem de validacao em portugues e nenhum link e criado.
3. **Given** que um endereco IP ja criou 60 links nos ultimos 60 minutos, **When** tenta
   criar outro, **Then** a criacao e recusada sem afetar os links existentes;
   apos a janela de uma hora, ela pode criar outro link.

---

### User Story 2 - Acessar link e contabilizar visitas (Priority: P1)

Como visitante, abro um link curto ativo e sou encaminhado ao destino; como criador,
posso saber quantas vezes ele foi acessado.

**Why this priority**: O redirecionamento e a contagem realizam a finalidade do link.

**Independent Test**: Com um link previamente criado, acessa-lo duas vezes e consultar
o total, confirmando destino e contagem igual a dois.

**Acceptance Scenarios**:

1. **Given** um link ativo com zero acessos, **When** um visitante o abre, **Then** e
   redirecionado para a URL original e a contagem passa a um.
2. **Given** um link com acessos registrados e sua chave de gerenciamento, **When** o
   criador consulta a contagem, **Then** recebe o total atual, sem reducao em relacao
   a consultas anteriores.
3. **Given** um link criado antes de reiniciar a aplicacao, **When** a aplicacao
   reinicia e o link e aberto ou consultado, **Then** o redirecionamento continua
   disponivel e a contagem anterior e preservada.

---

### User Story 3 - Personalizar codigo e validade (Priority: P2)

Como criador, escolho um codigo reconhecivel e, opcionalmente, uma data de expiracao
para controlar ate quando o link redireciona.

**Why this priority**: Personalizacao e validade acrescentam controle sem impedir o
fluxo basico de criacao e acesso.

**Independent Test**: Criar um link com codigo escolhido e expiracao futura,
acessa-lo antes do prazo e confirmar que depois do prazo nao redireciona nem soma acesso.

**Acceptance Scenarios**:

1. **Given** um codigo alfanumerico livre com 4 a 20 caracteres, **When** o criador
   o escolhe, **Then** o link criado usa exatamente esse codigo.
2. **Given** um codigo ja utilizado, **When** outra pessoa tenta utiliza-lo, **Then**
   a criacao e recusada e o link anterior permanece inalterado.
3. **Given** um link cuja expiracao foi atingida, **When** um visitante o abre,
   **Then** nao e redirecionado e o numero de acessos permanece igual.
4. **Given** uma expiracao no passado ou no instante da criacao, **When** o criador
   tenta criar o link, **Then** a criacao e recusada e nenhum link e criado.
5. **Given** um link criado sem expiracao, **When** ele e aberto em uma data posterior,
   **Then** continua a redirecionar e a contabilizar acessos.

### Edge Cases

- Uma colisao na geracao automatica exige novo codigo; nenhum codigo pode apontar
  para dois destinos, inclusive apos reinicio.
- Codigos personalizados com 3 ou 21 caracteres, espacos ou caracteres fora do
  alfabeto permitido sao recusados; os limites de 4 e 20 sao aceitos.
- URL relativa, malformada, sem host ou com esquema diferente de http/https e recusada;
  URLs validas com exatamente 2048 caracteres sao aceitas.
- Expiracao no instante exato do acesso ja impede redirecionamento e contagem.
- Acessos simultaneos a um link ativo aumentam a contagem uma vez por acesso;
  consultas e tentativas em links inexistentes ou expirados nao aumentam o total.
- Exceder o limite de criacao nao bloqueia redirecionamentos nem consultas a links existentes.

## Requirements _(mandatory)_

### Functional Requirements

- **FR-001**: O sistema MUST permitir criar link curto a partir de URL absoluta http
  ou https, com host e no maximo 2048 caracteres; entradas invalidas MUST ser recusadas.
- **FR-002**: Sem codigo escolhido, o sistema MUST gerar codigo alfanumerico de
  exatamente sete caracteres e MUST evitar reutilizar qualquer codigo existente.
- **FR-003**: O criador MUST poder escolher codigo de 4 a 20 caracteres usando
  somente o alfabeto permitido; codigos fora desses limites ou com outros
  caracteres MUST ser recusados.
- **FR-004**: Cada codigo MUST ser unico em todo o sistema, seja gerado ou escolhido;
  em caso de conflito, o sistema MUST recusar o codigo escolhido ou gerar outro
  automaticamente, sem substituir o link existente.
- **FR-005**: A criacao MUST devolver um link curto compartilhavel que resolve para
  a URL original completa, inclusive caminho, parametros e fragmento informados, e
  uma chave de gerenciamento exibida ao criador nessa resposta.
- **FR-006**: A criacao MUST aceitar expiracao futura opcional; sem expiracao, o
  link MUST continuar ativo indefinidamente.
- **FR-007**: Abrir codigo existente e ativo MUST redirecionar ao destino e registrar
  exatamente um acesso para cada abertura, inclusive sob acessos simultaneos.
- **FR-008**: Um link MUST expirar no instante configurado; a partir desse instante,
  MUST NOT redirecionar nem registrar acesso.
- **FR-009**: Codigos inexistentes MUST NOT redirecionar nem registrar acesso.
- **FR-010**: O sistema MUST permitir consultar o total de acessos de um codigo
  existente somente com a chave de gerenciamento correspondente; esse total MUST
  iniciar em zero e MUST NOT diminuir. Codigo sem chave continua publico para redirecionar.
- **FR-011**: Links, destinos, expiracoes e contagens MUST permanecer disponiveis
  e inalterados apos reiniciar a aplicacao.
- **FR-012**: Rejeicoes por entrada invalida MUST explicar o motivo em portugues.
- **FR-013**: Cadastro e login, dominio personalizado, relatorio geografico e
  edicao do destino apos a criacao MUST permanecer fora de escopo.
- **FR-014**: Cada endereco IP solicitante MUST poder criar no maximo 60 links em qualquer janela
  de 60 minutos; novas criacoes acima desse limite MUST ser recusadas ate que
  haja capacidade novamente. Redirecionamentos MUST NOT ter esse limite.
- **FR-015**: A chave de gerenciamento MUST ser gerada para cada link, devolvida ao
  criador somente na criacao e armazenada apenas como hash; consultas com chave
  ausente ou invalida MUST ser recusadas sem revelar a contagem.

### Key Entities _(include if feature involves data)_

- **Link curto**: Codigo unico, URL de destino, data de criacao, expiracao opcional,
  total de acessos acumulados e hash da chave de gerenciamento.
- **Acesso**: Abertura de um link ativo que contribui uma unidade para a contagem;
  tentativas de abrir links inexistentes ou expirados nao sao acessos contabilizados.

## Success Criteria _(mandatory)_

### Measurable Outcomes

- **SC-001**: Em teste com participantes, pelo menos 95% conseguem criar e
  compartilhar um link valido na primeira tentativa em ate 60 segundos.
- **SC-002**: Em 100 aberturas simultaneas de um mesmo link ativo, 100 visitantes
  sao encaminhados ao destino e a contagem aumenta exatamente em 100.
- **SC-003**: Em testes de fronteira, 100% dos links vencidos no momento da abertura
  deixam de encaminhar visitantes e mantem a contagem anterior.
- **SC-004**: Apos reinicio, 100% de uma amostra de 100 links criados antes dele
  conservam destino, validade e contagem e continuam acessiveis quando ativos.
- **SC-005**: Em um ambiente de referencia com a API e o armazenamento no mesmo host,
  apos 5 requisicoes de aquecimento, pelo menos 95 das proximas 100 aberturas
  sequenciais de um link ativo recebem a resposta de redirecionamento em ate 2 segundos.

## Assumptions

- Sem cadastro ou login, a chave de gerenciamento funciona como credencial de acesso
  a contagem; o codigo curto sozinho permite apenas redirecionar.
- Codigos gerados e personalizados usam o alfabeto
  `ABCDEFGHJKLMNPQRSTUVWXYZabcdefghijkmnpqrstuvwxyz23456789`, com distincao
  entre maiusculas e minusculas; I, O, l, o, 0 e 1 sao excluidos por ambiguidade visual.
- Expiracao opcional e informada como instante futuro com referencia de fuso horario;
  a comparacao ocorre no instante indicado, inclusive no limite exato.
- A aplicacao tem endereco publico configurado para montar o link compartilhavel;
  configurar dominio personalizado nao faz parte da feature.
- Nao ha exclusao automatica de links expirados nem edicao do destino apos criacao.
- Esta feature nao depende de cadastro, provedor externo de links ou servicos de
  terceiros; a disponibilidade do endereco publico e pre-requisito operacional.
