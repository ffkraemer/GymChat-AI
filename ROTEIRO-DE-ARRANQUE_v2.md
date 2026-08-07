# GymChat AI — Roteiro de Arranque

Guia rápido para pores a aplicação a correr do zero, sempre que voltares a trabalhar nela.
Segue os passos por ordem — cada um depende do anterior.

---

## ✅ Checklist rápido (se já sabes o processo de cor)

- [ ] Docker Desktop aberto
- [ ] `docker compose up -d` (raiz do repo)
- [ ] Confirmar se há migrações por aplicar (`dotnet ef migrations has-pending-model-changes`)
- [ ] `dotnet run --project src\GymChatAI.Api`
- [ ] `devtunnel host gymchat` (**noutra janela**) — ou `devtunnel host -p 5277 --allow-anonymous` se não usares o túnel persistente
- [ ] Confirmar se o URL do devtunnel mudou → se sim, atualizar Callback URL no Meta (com túnel persistente, o URL mantém-se e não precisas)
- [ ] `GET /health`, `GET /health/whatsapp`, `GET /health/ai` → todos `"ok"`
- [ ] (opcional) `cd frontend && npm run dev` se fores usar o Portal de Administração
- [ ] (só na primeira vez, por gym) Chaves RSA geradas + registadas, se fores usar **WhatsApp Flows**

---

## Passo 1 — Docker Desktop

Abre a aplicação **Docker Desktop** e espera que o ícone fique verde/estável (não basta o processo arrancar, tem de terminar de inicializar).

## Passo 2 — Subir o SQL Server

Na **raiz do repositório**:
```powershell
docker compose up -d
```

Confirma que está saudável:
```powershell
docker ps
```
Deve mostrar `gymchatai-sqlserver` com estado `healthy` (pode demorar uns 10-20s a passar de `starting` para `healthy` na primeira vez).

> Não uses `docker compose down -v` a menos que queiras mesmo apagar os dados — o `-v` remove o volume onde a base de dados vive. `docker compose down` (sem `-v`) ou simplesmente deixar os containers a correr são seguros.

## Passo 3 — Confirmar migrações pendentes (antes de arrancar)

```powershell
cd src\GymChatAI.Infrastructure
dotnet ef migrations has-pending-model-changes --startup-project ..\GymChatAI.Api
```
Se disser que há alterações pendentes:
```powershell
dotnet ef migrations add NomeDaAlteracao --startup-project ..\GymChatAI.Api
```
O `MigrateAsync()` no arranque aplica a migração sozinho — nunca precisas de `DROP DATABASE` para isto.

> **Migração mais recente:** `AddOptionLists` (listas de opções reutilizáveis e geríveis no Portal — entidades `OptionList` + `OptionListItem`). Se acabaste de puxar o código das listas de opções e ainda não a geraste, corre o comando acima com `AddOptionLists` como nome.

## Passo 4 — Arrancar o backend

```powershell
cd ..\..
dotnet run --project src\GymChatAI.Api
```
(ou usa o ▶ Play do Visual Studio)

Confirma no log:
```
GymChat AI started using SQL Server persistence (authentication enabled).
```

Se aparecer `"falling back to in-memory persistence"` em vez disso, a connection string não foi encontrada — confirma os `user-secrets`:
```powershell
cd src\GymChatAI.Api
dotnet user-secrets list
```

## Passo 5 — devtunnel (numa janela de terminal **separada**, deixa esta a correr)

> **Porquê devtunnel e não ngrok?** O ngrok foi removido pela segurança corporativa (sinalizado como potencialmente malicioso — comum em ambientes empresariais, porque cria túneis de saída). Passámos para o **devtunnel** da Microsoft, que costuma levantar menos suspeita. Ver a secção "Instalar o devtunnel" abaixo se ainda não o tiveres.

**Túnel persistente (recomendado — mantém o mesmo URL entre reinícios):**
```powershell
devtunnel host gymchat
```

Se ainda não criaste o túnel persistente, faz isto **uma vez**:
```powershell
devtunnel create --tunnel-name gymchat --allow-anonymous
devtunnel port create gymchat -p 5277
devtunnel host gymchat
```

**Ou túnel efémero (URL muda a cada arranque):**
```powershell
devtunnel host -p 5277 --allow-anonymous
```

⚠️ O `--allow-anonymous` é **essencial** — sem ele, o devtunnel exige login Microsoft para qualquer pedido, e a Meta não tem essa autenticação; os webhooks dela seriam sempre rejeitados.

Confirma a linha do URL:
```
Connect via browser: https://gymchat-5277.usw2.devtunnels.ms
```

### ⚠️ O URL só muda se NÃO usares o túnel persistente

Com o túnel persistente (`gymchat`), o URL mantém-se sempre igual e **não precisas de atualizar nada no Meta** entre sessões. Só se usares o túnel efémero (ou criares um túnel novo) é que tens de atualizar:

1. `developers.facebook.com` → a tua app → **Use cases → Customize → Basic setup → Step 2. Production setup**
2. **Configure Webhooks** → **Callback URL**: cola o novo URL + `/webhooks/whatsapp/` no fim
3. **Verify Token**: o mesmo de sempre (não muda)
4. **Verify and Save**

Se estiveres a usar **WhatsApp Flows**, o endpoint de *data exchange* (`/webhooks/whatsapp/flow-data-exchange`) também vive atrás do mesmo túnel devtunnel — não precisas de configurar nada extra para isso no Meta (o endpoint é referenciado no próprio Flow, não como um webhook geral), mas confirma que o devtunnel continua ativo sempre que testares um Flow.

## Passo 6 — Confirmar que tudo está saudável, antes de testares

```
GET http://localhost:5277/health
GET http://localhost:5277/health/whatsapp
GET http://localhost:5277/health/ai
```

Todos devem devolver `"status": "ok"` (ou `"healthy"` no caso do `/health` geral). Se algum disser `"expired"` ou `"error"`, resolve isso **antes** de tentares testar pelo WhatsApp — poupa tempo a diagnosticar o sítio errado.

| Se `/health/whatsapp` falhar | Se `/health/ai` falhar |
|---|---|
| Token expirado → gera um novo (permanente, via System User) | Chave inválida, revogada, ou (Gemini free) limite de taxa temporário |

## Passo 7 — (Opcional) Portal de Administração

```powershell
cd frontend
npm run dev
```
Abre `http://localhost:5173`. Login: `admin@demo.gymchat.ai` / `GymChat!Demo123`.

## Passo 8 — Testar

Envia uma mensagem de WhatsApp para o teu número de teste. Acompanha em paralelo:
- O terminal do `dotnet run` (logs do processamento)
- O terminal do `devtunnel host` (mostra os pedidos que passam pelo túnel)

> Nota: ao contrário do ngrok, o devtunnel não tem um Inspector web local (tipo `127.0.0.1:4040`). Para inspeção detalhada dos pedidos, usa os logs do próprio `dotnet run`, ou o separador Network do browser quando testares via Portal.

---

## 🛠️ Instalar o devtunnel (só na primeira vez)

O `winget`, `choco`, e os scripts de instalação remotos falharam ou estavam bloqueados nesta máquina. O método que funcionou foi o **download direto do binário** da Microsoft:

```powershell
mkdir C:\Tools -Force
cd C:\Tools
Invoke-WebRequest -Uri https://aka.ms/TunnelsCliDownload/win-x64 -OutFile devtunnel.exe
```

Depois adiciona `C:\Tools` ao PATH (ver o aviso crítico abaixo), fecha e reabre o PowerShell, e confirma:
```powershell
devtunnel --version
devtunnel user login
```
(o `user login` só é preciso uma vez — abre o browser para autenticares com conta Microsoft ou GitHub.)

### ⚠️ ARMADILHA CRÍTICA: o limite de ~2000 caracteres do PATH

Se adicionares uma pasta ao PATH e o comando **continuar a não ser reconhecido** mesmo depois de reiniciar o terminal, quase de certeza é isto: o PATH do utilizador ultrapassou o limite histórico de ~2047 caracteres do Windows, e adições novas ficam cortadas silenciosamente. Isto aconteceu-nos várias vezes (com o `openssl`, o `dotnet-ef`, e o próprio `devtunnel`).

**A causa:** fazer repetidamente `$env:Path + ";nova_pasta"` e guardar de volta duplica o PATH do sistema para dentro do PATH do utilizador, que vai inchando até rebentar o limite.

**A forma CERTA de adicionar ao PATH** (só ao do utilizador, sem duplicar o do sistema):
```powershell
$userPath = [Environment]::GetEnvironmentVariable("Path", "User")
[Environment]::SetEnvironmentVariable("Path", "$userPath;C:\Tools", "User")
```

**Para diagnosticar** (se um `.exe` que sabes que existe não é reconhecido):
```powershell
$env:Path.Length          # se estiver perto ou acima de 2000, é este o problema
& "C:\Tools\devtunnel.exe" --version   # testa direto: se funciona, o problema é mesmo só o PATH
```

---

## 🔑 Configuração única por gym: WhatsApp Flows (encriptação RSA)

Só precisas de fazer isto **uma vez por WABA** (não repetes a cada arranque) — mas fica aqui documentado porque, se alguma vez precisares de gerar chaves novas ou configurares um gym novo, é aqui que voltas.

### 1. Gera o par de chaves RSA (precisa de `openssl` instalado)

```bash
openssl genrsa -des3 -passout pass:UMA_PASSWORD_FORTE_AQUI -out private.pem 2048
openssl rsa -in private.pem -passin pass:UMA_PASSWORD_FORTE_AQUI -pubout -out public.pem
```

Isto cria:
- `private.pem` — **nunca sai da tua máquina/servidor**, nunca vai para o git
- `public.pem` — este sim, vai para a Meta (não é secreto)

### 2. Guarda a chave privada nos `user-secrets` (nunca no `appsettings.json`)

```powershell
cd src\GymChatAI.Api
dotnet user-secrets set "WhatsAppFlow:PrivateKeyPem" "$(Get-Content ..\..\private.pem -Raw)"
dotnet user-secrets set "WhatsAppFlow:PrivateKeyPassword" "UMA_PASSWORD_FORTE_AQUI"
```

### 3. Regista a chave pública na Meta

No Portal → página **Flows** → cola o conteúdo de `public.pem` no campo "Chave pública RSA" → **"Registar chave"**.
(Isto chama `POST /{WABA_ID}/whatsapp_business_encryption` automaticamente por ti — não precisas de ir ao Graph API Explorer.)

### 4. Cria e publica o primeiro Flow

Ainda na página **Flows**: dá um nome → **"Criar Flow"** → **"Publicar"** → **"Testar"** com o teu número verificado.

### ⚠️ Se mudares de máquina/ambiente

A chave privada só existe nos teus `user-secrets` locais — se mudares de computador ou reinstalares o SO, tens de repetir o Passo 2 com o mesmo `private.pem` (guarda-o num cofre de passwords, não só no disco). Se perderes o `private.pem` sem cópia, tens de gerar um par novo e registar a chave pública nova (as sessões de Flow antigas deixam de funcionar, mas isso é normal e esperado — sessões de Flow são de curta duração).

---

## 🔧 Referência rápida de problemas já resolvidos

| Sintoma | Causa provável | Solução |
|---|---|---|
| Webhook da Meta não chega à app | URL do devtunnel mudou, túnel não está a correr, ou falta `--allow-anonymous` | Passo 5 |
| Túnel a correr mas app não recebe | App não está a correr, ou porta errada | Confirma Passo 4 |
| `devtunnel` / `openssl` / `dotnet-ef` "not recognized" mesmo após reiniciar o terminal | PATH ultrapassou ~2000 caracteres e cortou a adição | Ver "ARMADILHA CRÍTICA do PATH" na secção de instalação |
| `"No gym configured for WhatsApp phone number id"` | `DemoPhoneNumberId` não bate certo, ou processo antigo ainda na porta | Confirma `appsettings`/secrets; mata processos antigos na porta 5277 |
| Erro 401 do WhatsApp ao enviar | Token expirado | `GET /health/whatsapp`; gera token permanente (System User) |
| `"AI assistant unavailable"` | Chave de IA inválida, ou limite de taxa (comum no Gemini free) | `GET /health/ai`; olha a linha de erro completa no log, acima da mensagem resumida |
| `"Business account is restricted from messaging users in this country"` | Restrição Brasil/Indonésia entre países diferentes | Usa destinatário de país não-restrito |
| CORS bloqueado no frontend | Falta `app.UseCors(...)` no backend | Confirma `Program.cs` |
| `Invalid object name 'X'` no SQL | Falta aplicar migração nova | `dotnet ef migrations add ... && dotnet run` (o `MigrateAsync()` aplica sozinho) |
| `PendingModelChangesWarning` ao arrancar | Alteraste uma entidade/configuração e ainda não geraste a migração correspondente | Passo 3 |
| Erro no primeiro request a um endpoint gym-scoped | `GymScopeFilter` a bloquear (gymId da rota ≠ claim do token) | Confirma que estás a usar o `gymId` certo para o utilizador autenticado |
| `421` no endpoint de *data exchange* das Flows | Falha a desencriptar (chave errada/desatualizada) | Confirma que a chave pública registada na Meta corresponde à privada nos `user-secrets` |
| Flow não avança além do ecrã inicial | `WhatsAppBusinessAccountId` do gym não está configurado, ou a chave não foi registada | Página **Templates**/**Flows** → confirma o WABA ID e regista a chave |

---

## Credenciais de referência

| O quê | Valor |
|---|---|
| Login do Portal de Administração | `admin@demo.gymchat.ai` / `GymChat!Demo123` |
| Password do SQL Server (dev) | `Your_password123` (definida no `docker-compose.yml`) |
| Onde renovar o token do WhatsApp | `business.facebook.com` → System Users → Generate Token (Never expire) |
| Onde renovar a chave do Gemini | `aistudio.google.com/apikey` |
| Onde gerar as chaves RSA das Flows | `openssl` local (ver secção acima) |

---

*Este documento é só para desenvolvimento local. Em produção, o arranque seria gerido por infraestrutura própria (não Docker Desktop/devtunnel manuais — a API estaria num URL público estável e real), e a chave privada RSA viveria num cofre de segredos gerido (ex: Azure Key Vault), não em `user-secrets`.*
