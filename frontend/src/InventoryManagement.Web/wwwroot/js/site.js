(() => {
  const progress = document.createElement('div');
  progress.className = 'page-progress';
  progress.setAttribute('aria-hidden', 'true');
  document.body.appendChild(progress);
  const status = document.querySelector('#navigation-status');
  const main = document.querySelector('#main');
  const startLoading = (region, message) => {
    document.body.classList.add('is-loading');
    if (status) status.textContent = message;
    if (region) region.setAttribute('aria-busy', 'true');
    if (region && region !== main && !region.querySelector('.region-loading')) {
      const notice = document.createElement('p');
      notice.className = 'region-loading';
      notice.setAttribute('aria-hidden', 'true');
      notice.textContent = message;
      region.appendChild(notice);
    }
  };

  document.querySelectorAll('[data-trim-max]').forEach((input) => {
    const validateLength = () => {
      const limit = Number(input.dataset.trimMax);
      input.setCustomValidity(input.value.trim().length > limit
        ? `Admite hasta ${limit} caracteres después de quitar espacios externos.` : '');
    };
    input.addEventListener('input', validateLength);
    input.addEventListener('change', validateLength);
    validateLength();
  });

  document.querySelectorAll('input[name="Quantity"]').forEach((input) => {
    const validateQuantity = () => {
      const value = input.value;
      const quantity = Number(value);
      input.setCustomValidity(value && (!/^[0-9]+$/.test(value) || !Number.isInteger(quantity) || quantity < 1 || quantity > 2147483647)
        ? 'La cantidad debe ser un entero entre 1 y 2147483647.' : '');
    };
    input.addEventListener('input', validateQuantity);
    input.addEventListener('change', validateQuantity);
    validateQuantity();
  });

  document.querySelectorAll('form[data-submitting]').forEach((form) => {
    form.addEventListener('submit', (event) => {
      if (!form.checkValidity()) { event.preventDefault(); form.reportValidity(); return; }
      if (form.dataset.busy === 'true') { event.preventDefault(); return; }
      form.dataset.busy = 'true';
      const button = form.querySelector('button[type="submit"]');
      if (button) {
        button.disabled = true;
        button.dataset.originalText = button.textContent;
        button.textContent = 'Procesando…';
      }
      startLoading(form, form.method.toLowerCase() === 'get' ? 'Cargando resultados…' : 'Procesando solicitud…');
    });
  });

  const price = document.querySelector('input[name="PriceText"]');
  if (price) {
    const validatePrice = () => {
      const value = price.value.trim();
      price.setCustomValidity(value && !/^(?:\d+)(?:\.\d{1,2})?$/.test(value)
        ? 'Usa punto y hasta dos decimales, por ejemplo 150.50.' : '');
    };
    price.addEventListener('input', validatePrice);
    price.addEventListener('change', validatePrice);
  }

  document.querySelectorAll('#startDateText, #endDateText').forEach((input) => {
    input.addEventListener('input', () => {
      const value = input.value.trim();
      input.setCustomValidity(value && !/(?:Z|[+-]\d{2}:\d{2})$/i.test(value)
        ? 'Incluye fecha, hora y zona, por ejemplo 2026-09-29T12:00:00-06:00.' : '');
    });
  });

  document.addEventListener('click', (event) => {
    const link = event.target.closest('a[href]');
    if (!link || event.defaultPrevented || event.button !== 0 || event.metaKey || event.ctrlKey || event.shiftKey || event.altKey || link.target === '_blank') return;
    const url = new URL(link.href, location.href);
    if (link.hasAttribute('download')) return;
    if (url.origin === location.origin && (url.pathname !== location.pathname || url.search !== location.search))
      startLoading(link.closest('.history-panel, .table-wrap, .pager') || main, 'Cargando página…');
  });
  window.addEventListener('pageshow', () => {
    document.body.classList.remove('is-loading');
    if (status) status.textContent = '';
    document.querySelectorAll('[aria-busy="true"]').forEach(region => region.setAttribute('aria-busy', 'false'));
    document.querySelectorAll('.region-loading').forEach(notice => notice.remove());
    document.querySelectorAll('form[data-busy]').forEach((form) => {
      form.dataset.busy = 'false';
      const button = form.querySelector('button[type="submit"]');
      if (button) { button.disabled = false; button.textContent = button.dataset.originalText || button.textContent; }
    });
  });
})();
