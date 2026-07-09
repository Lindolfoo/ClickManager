/**
 * Click Manager v2 – main.js
 * Todos os eventos declarados via JS (sem onclick/onsubmit no HTML)
 * Tema claro/escuro via data-theme no <html>
 * Comunicação com API via fetch (AJAX)
 */

/* ============================================================
   CONFIG
   ============================================================ */
var API = "http://localhost:5000/api";

/* ============================================================
   1. TEMA CLARO / ESCURO  (corrigido)
   ============================================================ */
(function initTheme() {
  var html = document.documentElement;
  var btn  = document.getElementById("themeToggle");
  var icon = document.getElementById("themeIcon");
  if (!btn) return;

  var saved = localStorage.getItem("cm-theme") ||
              (window.matchMedia("(prefers-color-scheme: dark)").matches ? "dark" : "light");
  applyTheme(saved);

  btn.addEventListener("click", function () {
    applyTheme(html.getAttribute("data-theme") === "dark" ? "light" : "dark");
  });

  function applyTheme(t) {
    html.setAttribute("data-theme", t);
    localStorage.setItem("cm-theme", t);
    if (icon) icon.className = t === "dark" ? "bi bi-sun-fill" : "bi bi-moon-fill";
    if (btn)  btn.setAttribute("aria-label", t === "dark" ? "Tema claro" : "Tema escuro");
  }
})();

/* ============================================================
   2. NAVEGAÇÃO SPA (troca de páginas sem recarregar)
   ============================================================ */
(function initNav() {
  // Decide qual layout está ativo (landing ou app)
  var isApp = !!document.getElementById("app-layout");
  if (!isApp) return;

  var navItems = document.querySelectorAll(".nav-item[data-page]");
  var pages    = document.querySelectorAll(".page");
  var topbarTitle = document.getElementById("topbarTitle");

  navItems.forEach(function (item) {
    item.addEventListener("click", function () {
      var target = this.getAttribute("data-page");
      navigateTo(target);
    });
  });

  // Botões que navegam dentro do conteúdo
  document.addEventListener("click", function (e) {
    var el = e.target.closest("[data-goto]");
    if (el) navigateTo(el.getAttribute("data-goto"));
  });

  function navigateTo(pageId) {
    pages.forEach(function (p) { p.classList.remove("active"); });
    navItems.forEach(function (n) { n.classList.remove("active"); });

    var targetPage = document.getElementById("page-" + pageId);
    var targetNav  = document.querySelector(".nav-item[data-page='" + pageId + "']");

    if (targetPage) {
      targetPage.classList.add("active");
      if (targetNav) targetNav.classList.add("active");
      if (topbarTitle) topbarTitle.textContent = targetPage.getAttribute("data-title") || "Click Manager";
      window.scrollTo(0,0);
      // Dispara evento customizado (outras partes do JS podem ouvir)
      document.dispatchEvent(new CustomEvent("pagechange", { detail: { page: pageId } }));
    }
  }

  // Ativa dashboard por padrão
  navigateTo("dashboard");
})();

/* ============================================================
   3. MOBILE SIDEBAR
   ============================================================ */
(function initMobileSidebar() {
  var toggle  = document.getElementById("sidebarToggle");
  var sidebar = document.getElementById("sidebar");
  if (!toggle || !sidebar) return;

  toggle.addEventListener("click", function () {
    sidebar.classList.toggle("open");
  });

  // Fecha ao clicar fora
  document.addEventListener("click", function (e) {
    if (!sidebar.contains(e.target) && e.target !== toggle) {
      sidebar.classList.remove("open");
    }
  });

  // Fecha ao navegar
  document.querySelectorAll(".nav-item[data-page]").forEach(function (item) {
    item.addEventListener("click", function () {
      sidebar.classList.remove("open");
    });
  });
})();

/* ============================================================
   4. DASHBOARD – carrega dados via AJAX do usuário logado
   ============================================================ */
(function initDashboard() {
  // Mostra nome do usuário logado na topbar
  var user = JSON.parse(localStorage.getItem("cm-user") || "{}");
  if (user && user.nome) {
    var avatar = document.querySelector(".topbar-actions div[style*='border-radius:50%']");
    if (avatar) {
      var initials = user.nome.split(" ").map(function(n){ return n[0]; }).join("").substring(0,2).toUpperCase();
      avatar.textContent = initials;
    }
  }

  document.addEventListener("pagechange", function (e) {
    if (e.detail.page !== "dashboard") return;
    loadDashboard();
  });

  function loadDashboard() {
    var token = localStorage.getItem("cm-token");
    var headers = token ? { "Authorization": "Bearer " + token } : {};

    // Verifica status da API
    fetch(API + "/contact/health")
      .then(function (r) { return r.json(); })
      .then(function () {
        var el = document.getElementById("apiStatus");
        if (el) { el.textContent = "Online"; el.className = "badge badge-success"; }
        var el2 = document.getElementById("apiStatusConf");
        if (el2) { el2.textContent = "Online"; el2.className = "badge badge-success"; }
      })
      .catch(function () {
        var el = document.getElementById("apiStatus");
        if (el) { el.textContent = "Offline"; el.className = "badge badge-danger"; }
      });

    // Carrega dados reais do dashboard
    fetch(API + "/ensaios/dashboard", { headers: headers })
      .then(function(r) { return r.json(); })
      .then(function(data) {
        // Atualiza stat cards com dados reais do usuário
        var stats = document.querySelectorAll(".stat-value");
        if (stats[0]) stats[0].textContent = data.proximosEnsaios  ?? data.ProximosEnsaios  ?? "0";
        if (stats[1]) stats[1].textContent = data.pagamentosPendentes ?? data.PagamentosPendentes ?? "0";
        if (stats[2]) stats[2].textContent = data.clientesAtivos    ?? data.ClientesAtivos    ?? "0";

        var total = data.totalRecebido ?? data.TotalRecebido ?? 0;
        if (stats[3]) stats[3].textContent = "R$" + Number(total).toLocaleString("pt-BR");

        // Atualiza agenda da semana
        var agenda = data.agendaSemana ?? data.AgendaSemana ?? [];
        var container = document.querySelector(".page.active .list-item:first-child")?.parentElement;
        if (agenda.length === 0 && container) {
          container.innerHTML = '<p style="color:var(--text2);font-size:.875rem;padding:16px 0">Nenhum ensaio nos próximos 7 dias.</p>';
        }
      })
      .catch(function() {
        // API offline — zera os valores para não mostrar dados de outros
        var stats = document.querySelectorAll(".stat-value");
        stats.forEach(function(s) { if(s) s.textContent = "—"; });
      });
  }
})();

/* ============================================================
   5. CALENDÁRIO (Agenda)
   ============================================================ */
(function initCalendar() {
  var monthEl = document.getElementById("calMonthLabel");
  var gridEl  = document.getElementById("calGrid");
  var prevBtn = document.getElementById("calPrev");
  var nextBtn = document.getElementById("calNext");
  if (!monthEl || !gridEl) return;

  var today   = new Date();
  var current = new Date(today.getFullYear(), today.getMonth(), 1);

  // Ensaios de exemplo (simula dados da API)
  var events = {
    [fmt(new Date(today.getFullYear(), today.getMonth(), 5))]:  "Festa 15 Anos Marcela",
    [fmt(new Date(today.getFullYear(), today.getMonth(), 12))]: "Casamento Virtor",
    [fmt(new Date(today.getFullYear(), today.getMonth(), 20))]: "Newborn",
    [fmt(today)]: "Hoje",
  };

  function fmt(d) {
    return d.getFullYear() + "-" +
           String(d.getMonth()+1).padStart(2,"0") + "-" +
           String(d.getDate()).padStart(2,"0");
  }

  function render() {
    var months = ["Janeiro","Fevereiro","Março","Abril","Maio","Junho","Julho","Agosto","Setembro","Outubro","Novembro","Dezembro"];
    monthEl.textContent = months[current.getMonth()] + " " + current.getFullYear();

    var firstDay = new Date(current.getFullYear(), current.getMonth(), 1).getDay();
    var daysInMonth = new Date(current.getFullYear(), current.getMonth()+1, 0).getDate();
    var daysInPrev  = new Date(current.getFullYear(), current.getMonth(), 0).getDate();

    gridEl.innerHTML = "";

    // Dias do mês anterior
    for (var i = firstDay - 1; i >= 0; i--) {
      var d = document.createElement("div");
      d.className = "cal-day other-month";
      d.textContent = daysInPrev - i;
      gridEl.appendChild(d);
    }

    // Dias do mês atual
    for (var day = 1; day <= daysInMonth; day++) {
      var d = document.createElement("div");
      var dateStr = current.getFullYear() + "-" +
                    String(current.getMonth()+1).padStart(2,"0") + "-" +
                    String(day).padStart(2,"0");

      var isToday = (dateStr === fmt(today));
      var hasEv   = !!events[dateStr];

      d.className = "cal-day" + (isToday ? " today" : "") + (hasEv ? " has-event" : "");
      d.textContent = day;

      if (hasEv) {
        d.title = events[dateStr];
        d.addEventListener("click", function(ev) {
          showToast("📅 " + ev.currentTarget.title, "info");
        });
      }
      gridEl.appendChild(d);
    }

    // Completar grid (6 linhas)
    var total = firstDay + daysInMonth;
    var remaining = total % 7 === 0 ? 0 : 7 - (total % 7);
    for (var j = 1; j <= remaining; j++) {
      var d = document.createElement("div");
      d.className = "cal-day other-month";
      d.textContent = j;
      gridEl.appendChild(d);
    }
  }

  if (prevBtn) prevBtn.addEventListener("click", function () {
    current.setMonth(current.getMonth() - 1);
    render();
  });
  if (nextBtn) nextBtn.addEventListener("click", function () {
    current.setMonth(current.getMonth() + 1);
    render();
  });

  document.addEventListener("pagechange", function (e) {
    if (e.detail.page === "agenda") render();
  });

  render();
})();

/* ============================================================
   6. FORMULÁRIO DE CONTATO / LANDING
      Envia via AJAX para a API ASP.NET Core
   ============================================================ */
(function initContactForm() {
  var btn  = document.getElementById("btnSubmit");
  var form = document.getElementById("contactForm");
  if (!btn || !form) return;

  var fields = {
    inputName:  function(v){ return v.trim().length >= 3; },
    inputEmail: function(v){ return /^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(v.trim()); },
    selectPlan: function(v){ return v !== ""; },
    checkTerms: function(){ return document.getElementById("checkTerms").checked; },
  };

  // Limpa validação ao editar
  Object.keys(fields).forEach(function(id) {
    var el = document.getElementById(id);
    if (!el) return;
    var ev = (el.tagName === "SELECT" || el.type === "checkbox") ? "change" : "input";
    el.addEventListener(ev, function(){ el.classList.remove("is-valid","is-invalid"); });
  });

  btn.addEventListener("click", function() {
    var ok = true;
    Object.keys(fields).forEach(function(id) {
      var el = document.getElementById(id);
      if (!el) return;
      var v = el.type === "checkbox" ? "x" : el.value;
      var passes = fields[id](v);
      el.classList.toggle("is-valid", passes);
      el.classList.toggle("is-invalid", !passes);
      if (!passes) ok = false;
    });
    if (!ok) return;

    var payload = {
      name:    document.getElementById("inputName").value.trim(),
      email:   document.getElementById("inputEmail").value.trim(),
      phone:   (document.getElementById("inputPhone") || {}).value || "",
      plan:    document.getElementById("selectPlan").value,
      message: (document.getElementById("inputMessage") || {}).value || "",
    };

    btn.disabled = true;
    btn.innerHTML = '<span class="spinner"></span> Enviando...';

    fetch(API + "/contact", {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify(payload),
    })
    .then(function(r){ return r.json().then(function(d){ return {ok:r.ok, d:d}; }); })
    .then(function(res) {
      btn.disabled = false;
      btn.textContent = "Enviar solicitação";
      if (res.ok && res.d.success) {
        showFormAlert("success", "✅ " + res.d.message);
        form.reset();
      } else {
        showFormAlert("danger", "⚠️ " + (res.d.message || "Erro ao enviar."));
      }
    })
    .catch(function() {
      btn.disabled = false;
      btn.textContent = "Enviar solicitação";
      showFormAlert("danger", "⚠️ Não foi possível conectar à API. Verifique se está rodando em localhost:5000.");
    });
  });

  function showFormAlert(type, msg) {
    var el = document.getElementById("formAlert");
    if (!el) return;
    el.className = "alert alert-" + type;
    el.textContent = msg;
    el.style.display = "block";
  }
})();

/* ============================================================
   7. MODAL HELPERS
   ============================================================ */
window.openModal = function(id) {
  var m = document.getElementById(id);
  if (m) m.classList.add("open");
};
window.closeModal = function(id) {
  var m = document.getElementById(id);
  if (m) m.classList.remove("open");
};

// Fecha ao clicar fora do modal
document.addEventListener("click", function(e) {
  if (e.target.classList.contains("modal-overlay")) {
    e.target.classList.remove("open");
  }
});

// Botões de modal via data-modal-open / data-modal-close
document.addEventListener("click", function(e) {
  var opener = e.target.closest("[data-modal-open]");
  if (opener) openModal(opener.getAttribute("data-modal-open"));
  var closer = e.target.closest("[data-modal-close]");
  if (closer) closeModal(closer.getAttribute("data-modal-close"));
});

/* ============================================================
   8. TOAST GLOBAL
   ============================================================ */
window.showToast = function(msg, type) {
  var container = document.getElementById("toastContainer");
  if (!container) return;
  var toast = document.createElement("div");
  toast.className = "toast" + (type ? " " + type : "");
  toast.textContent = msg;
  container.appendChild(toast);
  setTimeout(function() {
    toast.style.opacity = "0";
    toast.style.transition = "opacity .3s";
    setTimeout(function(){ toast.remove(); }, 300);
  }, 3500);
};

/* ============================================================
   9. LISTA DE ENSAIOS — carrega da API pelo usuário logado
   ============================================================ */
(function initEnsaios() {
  var ensaios = [];

  // Carrega da API sempre que a página de ensaios é aberta
  document.addEventListener("pagechange", function(e) {
    if (e.detail.page !== "ensaios") return;
    carregarEnsaios();
  });

  function carregarEnsaios() {
    var token = localStorage.getItem("cm-token");
    fetch(API + "/ensaios", {
      headers: token ? { "Authorization": "Bearer " + token } : {}
    })
    .then(function(r) { return r.json(); })
    .then(function(data) {
      ensaios = Array.isArray(data) ? data : [];
      renderEnsaios(ensaios);
    })
    .catch(function() {
      // API offline → lista vazia para o usuário (sem dados de outros)
      ensaios = [];
      renderEnsaios(ensaios);
    });
  }

  document.addEventListener("click", function(e) {
    if (e.target.closest("#btnNovoEnsaio")) openModal("modalNovoEnsaio");
  });

  document.addEventListener("click", function(e) {
    if (!e.target.closest("#btnSalvarEnsaio")) return;
    var titulo  = document.getElementById("ensaioTitulo")  ? document.getElementById("ensaioTitulo").value.trim()  : "";
    var cliente = document.getElementById("ensaioCliente") ? document.getElementById("ensaioCliente").value.trim() : "";
    var data    = document.getElementById("ensaioData")    ? document.getElementById("ensaioData").value    : "";
    var local   = document.getElementById("ensaioLocal")   ? document.getElementById("ensaioLocal").value.trim()   : "";
    var valor   = document.getElementById("ensaioValor")   ? document.getElementById("ensaioValor").value.trim()   : "";
    if (!titulo || !cliente || !data) { showToast("Preencha os campos obrigatórios.", "error"); return; }

    var token = localStorage.getItem("cm-token");
    fetch(API + "/ensaios", {
      method: "POST",
      headers: {
        "Content-Type": "application/json",
        "Authorization": token ? "Bearer " + token : ""
      },
      body: JSON.stringify({ titulo:titulo, clienteId:1, data:data, local:local, valor:parseFloat(valor)||0, status:"Agendado" })
    })
    .then(function() {
      closeModal("modalNovoEnsaio");
      carregarEnsaios();
      showToast("✅ Ensaio agendado com sucesso!", "success");
    })
    .catch(function() {
      showToast("Erro ao salvar. Verifique a API.", "error");
    });
  });

  function renderEnsaios(list) {
    var container = document.getElementById("ensaiosList");
    if (!container) return;
    container.innerHTML = "";
    if (!list || list.length === 0) {
      container.innerHTML = '<p style="color:var(--text2);text-align:center;padding:32px">Nenhum ensaio encontrado. Agende seu primeiro ensaio!</p>';
      return;
    }
    list.forEach(function(e) {
      var el = document.createElement("div");
      el.className = "list-item";
      el.innerHTML =
        '<div class="list-item-title">' + (e.titulo||e.Titulo||"") + '</div>' +
        '<div class="list-item-sub">' +
          '<span><i class="bi bi-person"></i>' + (e.cliente||e.Cliente||"") + '</span>' +
          '<span><i class="bi bi-calendar3"></i>' + (e.dataHora||e.DataHora||e.data||"").toString().substring(0,10) + '</span>' +
          '<span><i class="bi bi-geo-alt"></i>' + (e.local||e.Local||"") + '</span>' +
          '<span><i class="bi bi-file-text"></i>' + (e.contrato||e.Contrato||"") + '</span>' +
          '<span><i class="bi bi-currency-dollar"></i>R$ ' + (e.valor||e.Valor||0) + '</span>' +
          '<span class="badge ' + ((e.statusPgto||e.StatusPgto)==="Pago"?"badge-success":"badge-warning") + '">' + (e.statusPgto||e.StatusPgto||"Pendente") + '</span>' +
        '</div>';
      container.appendChild(el);
    });
  }
})();

/* ============================================================
   10. LISTA DE CLIENTES — carrega da API pelo usuário logado
   ============================================================ */
(function initClientes() {
  var clientes = [];

  document.addEventListener("pagechange", function(e) {
    if (e.detail.page !== "clientes") return;
    carregarClientes();
  });

  function carregarClientes() {
    var token = localStorage.getItem("cm-token");
    fetch(API + "/clientes", {
      headers: token ? { "Authorization": "Bearer " + token } : {}
    })
    .then(function(r) { return r.json(); })
    .then(function(data) {
      clientes = Array.isArray(data) ? data : [];
      renderClientes(clientes);
    })
    .catch(function() {
      clientes = [];
      renderClientes(clientes);
    });
  }

  document.addEventListener("click", function(e) {
    if (e.target.closest("#btnNovoCliente")) openModal("modalNovoCliente");
  });

  document.addEventListener("click", function(e) {
    if (!e.target.closest("#btnSalvarCliente")) return;
    var nome  = document.getElementById("clienteNome")  ? document.getElementById("clienteNome").value.trim()  : "";
    var email = document.getElementById("clienteEmail") ? document.getElementById("clienteEmail").value.trim() : "";
    var tipo  = document.getElementById("clienteTipo")  ? document.getElementById("clienteTipo").value  : "";
    if (!nome || !email) { showToast("Preencha nome e e-mail.", "error"); return; }

    var token = localStorage.getItem("cm-token");
    fetch(API + "/clientes", {
      method: "POST",
      headers: {
        "Content-Type": "application/json",
        "Authorization": token ? "Bearer " + token : ""
      },
      body: JSON.stringify({ nome:nome, email:email, tipoEnsaio:tipo, status:"Ativo" })
    })
    .then(function() {
      closeModal("modalNovoCliente");
      carregarClientes();
      showToast("✅ Cliente adicionado!", "success");
    })
    .catch(function() {
      showToast("Erro ao salvar. Verifique a API.", "error");
    });
  });

  function renderClientes(list) {
    var tbody = document.getElementById("clientesTbody");
    if (!tbody) return;
    tbody.innerHTML = "";
    if (!list || list.length === 0) {
      tbody.innerHTML = '<tr><td colspan="6" style="text-align:center;padding:32px;color:var(--text2)">Nenhum cliente cadastrado ainda.</td></tr>';
      return;
    }
    list.forEach(function(c) {
      var nome        = c.nome        || c.Nome        || "";
      var email       = c.email       || c.Email       || "";
      var tipo        = c.tipoEnsaio  || c.TipoEnsaio  || "";
      var status      = c.status      || c.Status      || "Ativo";
      var ultimoEnsaio = c.ultimoEnsaio || c.UltimoEnsaio;
      var dataFmt     = ultimoEnsaio
        ? new Date(ultimoEnsaio).toLocaleDateString("pt-BR")
        : "—";
      var tr = document.createElement("tr");
      tr.innerHTML =
        '<td><strong>' + nome + '</strong></td>' +
        '<td>' + email + '</td>' +
        '<td>' + tipo + '</td>' +
        '<td><span class="badge ' + (status==="Ativo"?"badge-success":"badge-warning") + '">' + status + '</span></td>' +
        '<td>' + dataFmt + '</td>' +
        '<td><button class="btn btn-ghost btn-sm">Editar</button></td>';
      tbody.appendChild(tr);
    });
  }
})();

/* ============================================================
   11. AJAX TICKER (dica do dia na landing)
   ============================================================ */
(function initTicker() {
  var el = document.getElementById("tickerContent");
  if (!el) return;
  var fallback = [
    "Organize seus ensaios com tags para encontrá-los mais rápido.",
    "Envie o contrato digital antes de confirmar qualquer sessão.",
    "Clientes satisfeitos indicam — peça avaliações após cada entrega.",
    "Acompanhe seu fluxo de caixa semanalmente para precificar melhor.",
  ];
  fetch("https://api.quotable.io/random?tags=inspirational&maxLength=100")
    .then(function(r){ return r.json(); })
    .then(function(d){ if(d && d.content) el.textContent = "\u201c" + d.content + "\u201d — " + d.author; else throw 0; })
    .catch(function(){ el.textContent = fallback[Math.floor(Math.random()*fallback.length)]; });
})();
