(function () {
  const button = document.querySelector('[data-menu-button]');
  const nav = document.querySelector('.site-nav');
  const header = button ? button.closest('.site-header') : null;
  const menuMedia = window.matchMedia('(max-width: 980px)');

  if (button && nav && header) {
    const navLinks = Array.from(nav.querySelectorAll('a[href]'));

    function setMenuOpen(open, options) {
      const settings = options || {};
      const shouldOpen = Boolean(open && menuMedia.matches);
      nav.classList.toggle('is-open', shouldOpen);
      button.setAttribute('aria-expanded', shouldOpen ? 'true' : 'false');

      if (shouldOpen && settings.focusFirst && navLinks[0]) {
        navLinks[0].focus();
      } else if (!shouldOpen && settings.restoreFocus) {
        button.focus();
      }
    }

    header.classList.add('is-menu-enhanced');
    button.hidden = false;
    setMenuOpen(false);

    button.addEventListener('click', function () {
      const isOpen = button.getAttribute('aria-expanded') === 'true';
      setMenuOpen(!isOpen, { focusFirst: !isOpen });
    });

    document.addEventListener('keydown', function (event) {
      if (event.key === 'Escape' && button.getAttribute('aria-expanded') === 'true') {
        event.preventDefault();
        setMenuOpen(false, { restoreFocus: true });
      }
    });

    document.addEventListener('click', function (event) {
      if (button.getAttribute('aria-expanded') === 'true' && !header.contains(event.target)) {
        setMenuOpen(false);
      }
    });

    nav.addEventListener('click', function (event) {
      if (event.target.closest('a')) {
        setMenuOpen(false);
      }
    });

    function handleMenuMediaChange(event) {
      if (!event.matches) setMenuOpen(false);
    }

    if (menuMedia.addEventListener) {
      menuMedia.addEventListener('change', handleMenuMediaChange);
    } else {
      menuMedia.addListener(handleMenuMediaChange);
    }
  }

  document.querySelectorAll('[data-company-application-form]').forEach(function (form) {
    const submitButton = form.querySelector('[data-company-application-submit]');
    const status = form.querySelector('[data-company-application-status]');
    const fallback = form.querySelector('[data-company-application-fallback]');
    const honeypot = form.querySelector('[data-company-application-honeypot]');
    let responseFrame = document.querySelector('[data-company-application-frame]');
    const defaultButtonText = submitButton ? submitButton.textContent : '提交申请';
    const minFillMs = Number(form.getAttribute('data-min-fill-ms')) || 2000;
    const timeoutMs = Number(form.getAttribute('data-timeout-ms')) || 15000;
    let openedAt = Date.now();
    let isSubmitting = false;
    let isAwaitingFrame = false;
    let timeoutId = null;
    let activeFrame = null;
    let frameSequence = 0;

    function setStatus(message, state) {
      if (!status) return;
      status.textContent = message;
      status.classList.remove('is-submitting', 'is-success', 'is-error');
      if (state) status.classList.add('is-' + state);
    }

    function restoreControls() {
      isSubmitting = false;
      form.removeAttribute('aria-busy');
      if (submitButton) {
        submitButton.disabled = false;
        submitButton.textContent = defaultButtonText;
      }
    }

    if (!submitButton || submitButton.disabled || !responseFrame || !fallback) return;
    fallback.hidden = true;

    function bindResponseFrame(frame) {
      frame.addEventListener('load', function () {
        if (frame !== activeFrame || !isSubmitting || !isAwaitingFrame) return;

        window.clearTimeout(timeoutId);
        activeFrame = null;
        isAwaitingFrame = false;
        form.reset();
        openedAt = Date.now();
        if (fallback) fallback.hidden = true;
        setStatus('申请已发送，我们将在审核后与您联系。', 'success');
        restoreControls();
      });
    }

    function replaceResponseFrame(onReady) {
      frameSequence += 1;
      responseFrame.remove();

      const nextFrame = document.createElement('iframe');
      nextFrame.className = 'company-application-response-frame';
      nextFrame.name = 'company-application-response-' + Date.now() + '-' + frameSequence;
      nextFrame.title = '企业入驻申请提交响应';
      nextFrame.hidden = true;
      nextFrame.setAttribute('data-company-application-frame', '');
      nextFrame.addEventListener('load', function handleInitialLoad() {
        nextFrame.removeEventListener('load', handleInitialLoad);
        bindResponseFrame(nextFrame);
        onReady();
      });
      nextFrame.src = 'about:blank';
      form.insertAdjacentElement('afterend', nextFrame);
      responseFrame = nextFrame;
      form.target = nextFrame.name;
    }

    bindResponseFrame(responseFrame);

    form.addEventListener('submit', function (event) {
      if (isSubmitting) {
        event.preventDefault();
        return;
      }

      if (honeypot && honeypot.value.trim() !== '') {
        event.preventDefault();
        setStatus('暂时无法提交，请稍后重试。', 'error');
        return;
      }

      if (Date.now() - openedAt < minFillMs) {
        event.preventDefault();
        setStatus('填写时间过短，请检查资料后再次提交。', 'error');
        return;
      }

      isSubmitting = true;
      isAwaitingFrame = true;
      activeFrame = responseFrame;
      form.setAttribute('aria-busy', 'true');
      if (submitButton) {
        submitButton.disabled = true;
        submitButton.textContent = '正在提交…';
      }
      if (fallback) fallback.hidden = true;
      setStatus('正在提交，请稍候。', 'submitting');

      timeoutId = window.setTimeout(function () {
        if (!isSubmitting || !isAwaitingFrame) return;

        activeFrame = null;
        isAwaitingFrame = false;
        setStatus('暂时无法确认提交状态，请保留当前资料并重试，或使用备用表单。', 'error');
        if (fallback) fallback.hidden = false;
        replaceResponseFrame(restoreControls);
      }, timeoutMs);
    });
  });

  const consentBanner = document.querySelector('[data-consent-banner]');
  const consentAccept = document.querySelector('[data-consent-accept]');
  const consentReject = document.querySelector('[data-consent-reject]');
  const consentStatus = document.querySelector('[data-consent-status]');
  const consentSettingsButtons = Array.from(document.querySelectorAll('[data-consent-settings]'));

  if (consentBanner && consentAccept && consentReject && window.SRBizConsent) {
    let returnFocus = null;

    function setConsentStatus(message) {
      if (consentStatus) consentStatus.textContent = message;
    }

    function showConsentBanner(options) {
      const settings = options || {};
      consentBanner.hidden = false;
      if (settings.focus) consentAccept.focus();
    }

    function hideConsentBanner(message) {
      consentBanner.hidden = true;
      setConsentStatus(message);

      const focusTarget = returnFocus || document.querySelector('#main-content');
      returnFocus = null;
      if (focusTarget && typeof focusTarget.focus === 'function') {
        try {
          focusTarget.focus({ preventScroll: true });
        } catch (_) {
          focusTarget.focus();
        }
      }
    }

    if (!window.SRBizConsent.current()) {
      showConsentBanner();
    }

    consentAccept.addEventListener('click', function () {
      window.SRBizConsent.acceptAnalytics();
      hideConsentBanner('已同意分析 Cookie。您可以随时通过页脚中的 Cookie 设置修改选择。');
    });

    consentReject.addEventListener('click', function () {
      window.SRBizConsent.rejectAnalytics();
      hideConsentBanner('已保持仅使用必要功能，分析 Cookie 未启用。');
    });

    consentSettingsButtons.forEach(function (settingsButton) {
      settingsButton.hidden = false;
      settingsButton.addEventListener('click', function () {
        returnFocus = settingsButton;
        showConsentBanner({ focus: true });
      });
    });

    consentBanner.addEventListener('keydown', function (event) {
      if (event.key === 'Escape' && window.SRBizConsent.current()) {
        event.preventDefault();
        hideConsentBanner('Cookie 设置未更改。');
      }
    });
  }
})();
