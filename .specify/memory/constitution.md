<!--
Sync Impact Report (temporary; remove before committing)
Version change: unratified scaffold -> 1.0.0
Modified principles:
- template principle 1 -> I. Separacao entre HTTP e negocio
- template principle 2 -> II. Persistencia isolada e substituivel
- template principle 3 -> III. Testes obrigatorios
- template principle 4 -> IV. Erros de validacao padronizados
- template principle 5 -> V. Dependencias justificadas
Added sections: Restricoes tecnicas e linguagem; Fluxo de desenvolvimento e revisao
Removed sections: none (template slots were filled)
Follow-up TODOs: none
-->

# Encurtador Constitution

## Core Principles

### I. Separacao entre HTTP e negocio

Cada endpoint de Minimal API MUST delegar acesso a dados a um servico de aplicacao;
endpoints MUST NOT acessar o banco diretamente. Regras de negocio MUST residir fora do endpoint
e de qualquer handler HTTP. Essa separacao permite testar regras sem infraestrutura HTTP.

### II. Persistencia isolada e substituivel

Persistencia MUST usar EF Core. O provider MUST ser substituivel por configuracao e
implementacoes de infraestrutura, sem alterar regras de negocio ou endpoints. SQL MUST NOT
ficar espalhado pela aplicacao; consultas e comandos de persistencia MUST ficar encapsulados
na camada de persistencia. Isso evita acoplamento da aplicacao a um banco especifico.

### III. Testes obrigatorios

Cada regra de negocio MUST ter teste unitario com xUnit. Cada endpoint MUST ter ao menos um
teste de integracao para seu caminho feliz. Mudancas nas regras ou endpoints MUST atualizar
os testes correspondentes; a revisao MUST confirmar essas coberturas antes da entrega.

### IV. Erros de validacao padronizados

Toda falha de validacao de entrada MUST retornar HTTP 400 com corpo ProblemDetails.
Endpoints e handlers MUST manter esse contrato mesmo quando a validacao ocorrer em
servicos de aplicacao. Isso fornece um formato previsivel aos consumidores da API.

### V. Dependencias justificadas

Toda dependencia nova MUST ter justificativa escrita no plano antes de ser adicionada.
A justificativa MUST indicar a finalidade e por que as dependencias existentes nao bastam.
Isso torna auditavel o custo de manutencao de cada adicao.

## Restricoes tecnicas e linguagem

- A API MUST usar .NET 10 e Minimal APIs.
- Nomes de classes e arquivos de codigo MUST estar em ingles.
- Mensagens de erro apresentadas aos consumidores da API MUST estar em portugues.

## Fluxo de desenvolvimento e revisao

- O plano MUST ser corrigido quando contradizer esta constituicao; a constituicao prevalece.
- Revisoes MUST verificar a separacao de responsabilidades, o contrato de validacao,
  os testes exigidos e as justificativas de dependencias no plano.
- Commits MUST seguir `tipo(escopo): descricao` e incluir o identificador do requisito
  na mensagem, por exemplo, `feat(api): cria link curto (REQ-001)`.

## Governance

Esta constituicao prevalece sobre planos, especificacoes e praticas conflitantes.
Alteracoes MUST ser propostas e revisadas com justificativa, impacto nos planos existentes
e registro da nova versao e data de emenda antes de sua adocao. A revisao de cada entrega
MUST verificar conformidade com os principios; desvios MUST ser resolvidos no plano ou
por emenda aprovada antes da implementacao. A versao segue SemVer: MAJOR para remocao ou
redefinicao incompativel de principios, MINOR para novos principios/secoes ou ampliacao
material, PATCH para esclarecimentos sem mudanca semantica.

**Version**: 1.0.0 | **Ratified**: 2026-10-19 | **Last Amended**: 2026-10-19
