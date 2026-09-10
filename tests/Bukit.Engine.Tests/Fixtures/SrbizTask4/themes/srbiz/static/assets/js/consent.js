(function () {
  'use strict';

  const storageKey = 'srbiz.analytics-consent.v1';
  const denied = Object.freeze({
    analytics_storage: 'denied',
    ad_storage: 'denied',
    ad_user_data: 'denied',
    ad_personalization: 'denied'
  });
  const analyticsGranted = Object.freeze({
    analytics_storage: 'granted',
    ad_storage: 'denied',
    ad_user_data: 'denied',
    ad_personalization: 'denied'
  });

  function ensureGtag() {
    window.dataLayer = window.dataLayer || [];
    window.gtag = window.gtag || function () {
      window.dataLayer.push(arguments);
    };
  }

  function updateConsent(values) {
    ensureGtag();
    window.gtag('consent', 'update', values);
  }

  function readChoice() {
    try {
      const record = JSON.parse(localStorage.getItem(storageKey));
      if (record && (record.value === 'analytics-granted' || record.value === 'analytics-denied')) {
        return record.value;
      }
    } catch (_) {
      // Keep the privacy-safe denied defaults when browser storage is unavailable.
    }
    return null;
  }

  function saveChoice(value) {
    try {
      localStorage.setItem(storageKey, JSON.stringify({
        value: value,
        updatedAt: new Date().toISOString()
      }));
    } catch (_) {
      // The current page can still honor the choice when persistence is unavailable.
    }
  }

  function choose(value, consent) {
    updateConsent(consent);
    saveChoice(value);
  }

  window.SRBizConsent = Object.freeze({
    acceptAnalytics: function () {
      choose('analytics-granted', analyticsGranted);
    },
    rejectAnalytics: function () {
      choose('analytics-denied', denied);
    },
    current: readChoice
  });

  const savedChoice = readChoice();
  if (savedChoice === 'analytics-granted') {
    updateConsent(analyticsGranted);
  } else if (savedChoice === 'analytics-denied') {
    updateConsent(denied);
  }
})();
