## Prompts

### 1.Constitution

**Projeto de API em .NET 10 com Minimal APIs.**

Princípios que quero fixar:

- Endpoint nunca acessa banco diretamente, sempre via serviço de aplicação
- Regra de negócio não vive no endpoint nem no handler HTTP
- Persistência com EF Core, provider trocável, sem SQL espalhado pela aplicação
- Toda regra de negócio precisa de teste unitário com xUnit
- Todo endpoint precisa de ao menos um teste de integração de caminho feliz
- Erro de validação retoma 400 com ProblemDetails, sempre
- Nenhuma dependência nova entra sem justificativa escrita no plano
- Nomes de classe e arquivo em inglês, mensagens de erro em português
- Commit no formato tipo(escopo): descrição, com o identificador do requisito

Se o plano contradisser algum princípio, quem muda é o plano.

### 2.Specify

**Um encurtador de links.**
O criador informa uma URL longa e recebe um link curto para compartilhar.
Quem clica no link curto é redirecionado ao destino, e cada acesso é contado.
O criador consegue consultar quantos acessos o link dele recebeu.
Comportamentos esperados:

- o código do link é gerado automaticamente com 7 caracteres alfanuméricos
- o criador pode escolher o próprio código, entre 4 e 20 caracteres
- o código é único em todo o sistema
- a URL de destino precisa ser http ou https e ter no máximo 2048 caracteres
- o criador pode definir uma data de expiração
- link expirado não redireciona e não conta acesso
- o contador nunca diminui
- os links continuam válidos depois que a aplicação reinicia
  Fora de escopo: cadastro e login de usuários, domínio personalizado,relatório geográfico, edição do destino de um link já criado.

### 3. Clarify

Nesta seção, esclareço dúvidas e detalhes adicionais sobre o projeto, garantindo que todos os requisitos e comportamentos esperados estejam bem definidos antes de iniciar o planejamento e a implementação.

### 4. Plan

.NET 10, Minimal APIs, um arquivo de endpoints por recurso.
EF Core com SQLite nesta versão, provider trocável para PostgreSQL depois.
Camada de aplicação com LinkService, dona das regras de negócio.
Testes com xUnit
Decisões que quero registradas com justificativa:

- unicidade do código garantida por índice único no banco, além da checagem
  no serviço, por causa de concorrência
- contador incrementado por update atômico, nunca lido e reescrito na aplicação
- chave de gerenciamento armazenada como hashi nunca em texto puro
- alfabeto do código sem caracteres ambíguo

### 5. Tasks

Caso não adicione no contexto o arquivo plan.md no contexto ele pega o ultimo gerado

### 6. Analize

Nesta seção, reviso o plano e as decisões tomadas, verificando se estão alinhadas com os princípios definidos nas seções. Identifico possíveis melhorias, riscos e inconsistências, garantindo que o projeto siga as diretrizes estabelecidas.

### 7. Implement

Nesta seção, descrevo a implementação do projeto com base no plano definido anteriormente. Incluo detalhes sobre a estrutura de arquivos, endpoints, serviços de aplicação, persistência e testes, garantindo que todas as decisões estejam alinhadas com os princípios estabelecidos.
