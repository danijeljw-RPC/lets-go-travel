---
document_type: issue-answer
issue_id: OI-0006
title: LiteAPI Payment SDK, Mobile Deferral and PCI Scope Decision
status: decision-recorded
created: 2026-07-28
updated: 2026-07-28
decision_owners:
  - Finance
  - Security
  - Compliance
related_adrs:
  - ADR-0007
related_docs:
  - OI-0006-mobile-payment-and-pci-scope.md
  - docs/security/payment-and-pci-scope.md
  - docs/applications/client-strategy.md
blocked_by:
  - OI-0002
---

<!-- markdownlint-disable MD013 MD025 -->

# OI-0006 Answer — LiteAPI Payment SDK, Mobile Deferral and PCI Scope

## Original requirement

| Review | Evidence or approval required | Owner | Effect |
| --- | --- | --- | --- |
| OI-0006 payment/PCI | Supported Blazor/browser component, mobile deferral, return behaviour, 3-D Secure, Australian payment methods, provider AOC and qualified PCI scope. | Finance, Security, Compliance | Blocks production checkout activation. |

## Executive answer

The selected implementation is the **LiteAPI/Nuitee Connect browser payment SDK or another LiteAPI-approved hosted payment component**.

For the .NET 10 Blazor web application:

- readytogo.travel initiates a LiteAPI prebook using `usePaymentSdk: true`.
- LiteAPI returns a `secretKey`, `transactionId` and `prebookId`.
- The browser loads LiteAPI's JavaScript payment SDK.
- The SDK replaces a target DOM element with the secure payment-provider portal.
- Cardholder data is entered into and handled by the LiteAPI-managed payment component and its underlying PCI-compliant payment provider.
- readytogo.travel does not create a custom card form.
- readytogo.travel does not send primary account numbers, expiry dates or card security codes through its API.
- readytogo.travel retains only the booking/payment references required to complete and reconcile the booking.
- After payment, the browser returns to an approved readytogo.travel HTTPS URL and readytogo.travel completes the booking through LiteAPI using the matching `transactionId` and `prebookId`.

This is **Option B — approved embedded provider component**, with an approved hosted redirect as the fallback if LiteAPI requires it for a particular payment method or platform.

The later native Android/iOS application is explicitly deferred. It will use an officially supported native provider SDK or a system-browser hosted payment flow. Unsupported embedded web views and custom native card-entry forms are prohibited.

The payment architecture is therefore selected. Production checkout remains disabled until the compliance and production evidence listed in this document is approved.

## Decision

### Web

Use the official LiteAPI Payment SDK as the default checkout component.

A direct LiteAPI-supported Stripe Payment Element integration may be used only where:

- LiteAPI authorises the account and provides the required publishable key;
- the integration remains on LiteAPI's Stripe platform;
- LiteAPI continues to create, control and capture the PaymentIntent;
- Security and Compliance approve the implementation; and
- the resulting PCI scope is no broader than the approved LiteAPI SDK route.

The direct Stripe alternative is not required for MVP merely to obtain more visual control.

### Mobile

Native mobile checkout is deferred.

When mobile development begins, the allowed order of preference is:

1. an official LiteAPI-supported native payment SDK;
2. an official underlying-provider native SDK explicitly approved by LiteAPI;
3. a system-browser hosted payment flow with universal-link/app-link return handling; or
4. no native checkout until one of the above is supported.

An embedded mobile web view containing the browser payment form is not approved unless LiteAPI and a qualified PCI reviewer explicitly approve that platform and flow.

### Merchant of record

The merchant-of-record and commercial allocation remain governed by OI-0002.

This document assumes the LiteAPI/Nuitee-managed customer payment model is selected for the applicable checkout route. It does not independently determine:

- the legal merchant of record;
- settlement ownership;
- customer statement descriptor;
- refund funding;
- dispute and chargeback allocation; or
- tax responsibility.

Production activation requires consistency between this technical payment design and the executed commercial model.

## Why the Blazor/browser route is supported

LiteAPI documents a browser JavaScript SDK loaded from:

```html
<script src="https://payment-wrapper.liteapi.travel/dist/liteAPIPayment.js?v=a1"></script>
```

The SDK is configured with:

- a sandbox or live environment indicator;
- a target DOM element;
- the `secretKey` returned by prebook;
- appearance and business-display settings; and
- an HTTPS `returnUrl`.

The SDK creates the secure payment portal inside the target element and redirects the browser to the configured return URL after a successful payment.

Blazor can host this component because Blazor supports ordinary browser JavaScript and DOM integration through JavaScript interop. LiteAPI does not publish a Blazor-specific package, but none is required for the documented browser SDK pattern.

This conclusion is a technical compatibility assessment based on the documented JavaScript integration. It is not a LiteAPI statement naming Blazor as a separately certified framework.

## Payment data flow

```text
Customer browser
    |
    | 1. Requests checkout
    v
readytogo.travel web/API
    |
    | 2. Calls LiteAPI prebook with usePaymentSdk: true
    v
LiteAPI
    |
    | 3. Returns prebookId, transactionId and secretKey
    v
readytogo.travel browser session
    |
    | 4. Mounts LiteAPI Payment SDK
    v
LiteAPI-managed payment component / underlying processor
    |
    | 5. Customer enters card or eligible wallet details
    | 6. Provider performs authentication and payment confirmation
    v
readytogo.travel HTTPS return URL
    |
    | 7. Recovers the server-side checkout session
    | 8. Verifies expected payment/booking state
    | 9. Calls LiteAPI book with prebookId and transactionId
    v
Confirmed booking or controlled recovery workflow
```

## Data-handling boundary

### readytogo.travel may handle

- internal checkout-session identifier;
- LiteAPI `prebookId`;
- LiteAPI `transactionId`;
- LiteAPI payment status or booking status;
- amount and currency;
- non-sensitive card presentation information returned by an approved provider, if any;
- customer and traveller details required for the booking;
- failure and reconciliation metadata; and
- audit records that do not contain prohibited cardholder data.

### readytogo.travel must not handle

- full primary account number;
- card security code or CVC/CVV;
- magnetic-stripe or equivalent track data;
- PIN or PIN block;
- raw wallet payment credentials;
- unrestricted payment-provider client secrets in logs;
- card-entry fields implemented by readytogo.travel;
- card data posted to readytogo.travel endpoints; or
- screenshots, telemetry or session replay containing payment fields.

## LiteAPI PCI statement

LiteAPI publicly states that:

- Nuitee Connect does not store, process or transmit raw payment card data;
- card payments are handled by PCI-compliant third-party payment providers using a PCI proxy or redirection model;
- sensitive cardholder data does not pass through Nuitee Connect systems;
- Nuitee Connect's own PCI scope is limited;
- Nuitee Connect aligns with SAQ A requirements under PCI DSS v4.x; and
- customers remain responsible for their own PCI DSS obligations based on their integration model.

This supports the selected architecture, but it is not a substitute for account-specific evidence or readytogo.travel's own PCI validation.

## Expected readytogo.travel PCI scope

The expected target is **SAQ A or the acquirer-approved equivalent for a fully outsourced card-not-present e-commerce payment channel**.

That target is reasonable only where all of the following remain true:

- all cardholder-data functions are outsourced to PCI DSS-compliant providers;
- readytogo.travel does not electronically store, process or transmit cardholder data;
- payment-form elements originate directly from the compliant payment provider;
- no readytogo.travel component can read payment-field values;
- the implementation follows LiteAPI's approved integration instructions;
- the provider confirms that its embedded payment form includes the controls required for SAQ A eligibility;
- the provider's current PCI DSS Attestation of Compliance covers the actual service being used; and
- readytogo.travel's acquirer, payment brand or qualified PCI reviewer confirms the applicable SAQ and scope.

Using LiteAPI's SDK reduces the scope. It does **not** mean readytogo.travel has no PCI DSS obligations.

The final SAQ must not be declared solely from this architecture document.

## Provider evidence required

Before production checkout activation, obtain:

1. LiteAPI/Nuitee's current PCI DSS Attestation of Compliance or equivalent compliance evidence.
2. Confirmation of the entity and service covered by the AOC.
3. Confirmation that the payment SDK/payment wrapper used by readytogo.travel is included in that assessed service.
4. Identification of the underlying payment processor and confirmation that its relevant services are PCI DSS compliant.
5. A PCI responsibility matrix identifying which party owns each applicable requirement.
6. Confirmation that card-entry elements originate directly from the compliant payment provider.
7. Confirmation that readytogo.travel cannot access raw cardholder data through the SDK.
8. Confirmation of payment-page script-security controls relevant to the embedded flow.
9. Confirmation of the supported production domains, currencies and countries.
10. Confirmation of the supported browser matrix.
11. Confirmation of the 3-D Secure flow and liability/authentication behaviour.
12. Confirmation of the wallet and payment methods enabled for an Australian point of sale.
13. The AOC expiry/assessment date and annual evidence-review process.
14. Written confirmation that material changes to the payment architecture will be notified.

LiteAPI states that it does not publicly publish audit reports, but reasonable security and compliance information may be shared with customers under appropriate confidentiality terms. The AOC should therefore be requested through the account or security-review channel.

## Browser and Blazor implementation

### Component model

The payment SDK must be wrapped behind a readytogo.travel payment-component abstraction.

The Blazor component owns:

- checkout-session creation;
- the LiteAPI prebook call through the readytogo.travel API;
- DOM target lifecycle;
- JavaScript SDK initialisation;
- non-sensitive progress and error presentation;
- navigation to and recovery from the return route; and
- cancellation/abandonment messaging.

The LiteAPI SDK or approved provider component owns:

- payment-field rendering;
- raw card or wallet credential capture;
- card validation;
- payment authentication;
- 3-D Secure challenge presentation;
- provider payment confirmation; and
- redirection to the configured return URL.

### JavaScript isolation

Use a small first-party JavaScript module to call the LiteAPI SDK through Blazor JavaScript interop.

The module must not:

- inspect payment iframe/form values;
- attach key or input listeners to payment fields;
- proxy card data;
- log SDK configuration containing secrets;
- persist the payment client secret;
- add payment secrets to URLs; or
- copy the provider form into readytogo.travel-controlled HTML fields.

### Content Security Policy

Production checkout must use a restrictive Content Security Policy that permits only the exact LiteAPI and underlying-provider origins required by the approved SDK.

The approved origin list must cover only necessary:

- scripts;
- frames;
- connections;
- images;
- styles; and
- form/navigation destinations.

Do not broadly permit `*`, arbitrary third-party scripts or unrestricted inline script execution on checkout pages.

Where the provider cannot supply a stable Subresource Integrity hash because the hosted script changes, document the exception and apply compensating script inventory, CSP and payment-page change-detection controls required by the qualified PCI assessment.

## Checkout-session handling

Create a server-side checkout session before mounting the SDK.

Store:

- internal checkout ID;
- authenticated user/guest correlation;
- `prebookId`;
- `transactionId`;
- expected amount;
- expected currency;
- offer identifier;
- expiry;
- status;
- return URL nonce/state;
- creation timestamp; and
- final booking identifier where completed.

The browser should receive only the minimum values needed by the payment component.

The `secretKey` or PaymentIntent client secret:

- may be delivered to the paying browser;
- must be scoped to that checkout;
- must not be logged;
- must not be placed in the return URL by readytogo.travel;
- must not be persisted in analytics, local storage or browser telemetry;
- must not be reused across a new prebook; and
- must be discarded when the checkout completes or expires.

## Return behaviour

LiteAPI documents that the browser is redirected to the configured `returnUrl` after payment confirmation. The matching `transactionId` and `prebookId` are then used to finalise the booking.

The return endpoint must be an HTTPS route on an approved production domain.

The return route must not trust browser navigation as proof that payment succeeded.

It must:

1. validate the checkout-session identifier and anti-forgery state;
2. recover `prebookId` and `transactionId` from server-side state;
3. verify that the expected authenticated user or guest owns the checkout session;
4. verify expected amount, currency and offer correlation;
5. determine payment state using the supported LiteAPI/provider mechanism;
6. call the booking endpoint idempotently;
7. handle an already-completed booking safely;
8. handle payment processing/pending status;
9. handle payment failure and allow a controlled retry;
10. handle payment success followed by booking failure;
11. record the final result;
12. prevent duplicate booking calls; and
13. redirect the customer to a stable confirmation, pending or recovery page.

### Duplicate return

Repeated browser returns, page refreshes and browser back/forward navigation must not create a second booking.

The internal checkout ID and LiteAPI references must be protected by a single-completion/idempotency boundary.

### Abandonment

If the customer abandons before payment:

- expire the checkout session;
- do not call the booking endpoint; and
- permit a new prebook when the customer retries.

If payment is confirmed but booking finalisation is abandoned or fails:

- preserve the checkout record;
- attempt controlled recovery;
- reconcile the transaction and booking status;
- show a pending/recovery message rather than asking for immediate duplicate payment; and
- escalate unresolved cases under the booking-failure process.

LiteAPI's documentation contains differing public statements about payment-hold release timing, including one hour in the newer direct Stripe guide and one to two business days in other SDK/troubleshooting text. readytogo.travel must not promise a fixed release time until LiteAPI confirms the production behaviour for the selected SDK and account.

## 3-D Secure

### Required behaviour

The selected payment component must own 3-D Secure and any required strong customer authentication challenge.

readytogo.travel must not:

- implement its own 3-D Secure challenge UI;
- collect authentication credentials;
- assume a frictionless outcome; or
- finalise a booking while the payment is still awaiting required customer action.

### Public evidence

LiteAPI's direct Stripe Payment Element documentation explicitly states that `stripe.confirmPayment` handles 3-D Secure/SCA challenges when required by the issuing bank.

It provides a sandbox test card that triggers a 3-D Secure challenge and instructs integrations to complete an end-to-end prebook, payment, redirect and book test before production.

The wrapped LiteAPI Payment SDK documentation does not separately describe its 3-D Secure test behaviour in the same detail. Therefore, the wrapped SDK route must be sandbox-tested with a LiteAPI-approved 3-D Secure scenario or LiteAPI must provide written confirmation that the SDK handles the challenge.

### Required test cases

- frictionless card success;
- challenged 3-D Secure success;
- challenged 3-D Secure failure;
- customer abandons the challenge;
- issuer/card decline;
- payment remains processing;
- return URL opened multiple times;
- browser closed after payment but before booking;
- payment success followed by booking failure;
- expired prebook/client secret; and
- mismatched sandbox/live configuration.

## Australian payment methods

### Publicly documented

LiteAPI publicly documents:

- customer credit-card payment; and
- Google Pay as an available alternative in the LiteAPI Payment SDK flow.

LiteAPI's direct Stripe Payment Element documentation states that eligible wallets such as Google Pay and Apple Pay may be displayed where enabled, and that payment-method availability varies by currency and region. It requires production-domain verification with LiteAPI for Apple Pay.

### Not publicly established for the selected wrapped SDK

The reviewed public documentation does not establish account-level Australian support for:

- Australian eftpos cards as a distinct payment rail;
- PayTo;
- BPAY;
- PayPal;
- Afterpay;
- Zip;
- direct debit;
- bank transfer as a customer checkout method; or
- any other Australia-specific alternative payment method.

The platform must not advertise a payment method merely because the underlying processor may support it generally.

### Product decision

For MVP:

- support cards through the LiteAPI SDK;
- display Google Pay, Apple Pay or another wallet only where the production SDK actually offers it for the customer, browser, device, currency and account;
- do not hard-code wallet availability;
- do not promise Australian alternative payment methods that LiteAPI has not enabled and confirmed; and
- treat additional local methods as later capability-gated enhancements.

The absence of PayTo, BPAY, eftpos-specific routing or buy-now-pay-later methods does not block MVP unless Product or Finance separately makes one of those methods a launch requirement.

## Mobile deferral

The initial product is the Blazor browser application.

The future Android/iOS application may call the readytogo.travel API for travel functions, but payment collection remains subject to a separately approved mobile route.

Until that review is complete:

- native checkout is disabled;
- payment SDK secrets are not handed to an unsupported native UI;
- browser JavaScript is not embedded in an arbitrary application web view;
- mobile application code does not collect or proxy card data;
- deep-link return behaviour is not assumed; and
- the system-browser flow may be adopted as the safe fallback.

This explicit deferral satisfies the MVP architecture requirement without expanding PCI scope prematurely.

## Logging, analytics and support controls

The checkout route must prevent payment data leakage through:

- application logs;
- exception reports;
- distributed traces;
- analytics events;
- browser session replay;
- customer-support screenshots;
- crash reporting;
- reverse-proxy access logs;
- query strings;
- referrer headers; and
- customer service notes.

At minimum:

- disable or mask session replay on checkout and return pages;
- redact `secretKey`, PaymentIntent client secrets and authentication values;
- never record CVC/CVV;
- do not log the full request body from the provider component;
- restrict support staff to non-sensitive transaction references;
- apply least-privilege access to checkout and payment records; and
- retain payment references according to the financial and booking-record policy.

## Production activation evidence

Production checkout may be activated only when:

- [ ] OI-0002 has approved the LiteAPI/Nuitee merchant-of-record and commercial model.
- [ ] The official LiteAPI SDK or approved hosted component has been selected for production.
- [ ] The Blazor wrapper has passed sandbox testing.
- [ ] The production browser/domain matrix is recorded.
- [ ] The return URL is HTTPS and production-approved.
- [ ] Duplicate return and idempotent booking behaviour have been tested.
- [ ] Abandoned checkout and payment-success/booking-failure recovery have been tested.
- [ ] 3-D Secure success, failure and abandonment have been tested.
- [ ] Enabled Australian payment methods have been recorded from the production account.
- [ ] Unsupported Australian payment methods are not advertised.
- [ ] LiteAPI/Nuitee's current PCI AOC or acceptable equivalent evidence has been reviewed.
- [ ] The underlying payment-provider responsibility is covered by the evidence or responsibility matrix.
- [ ] A qualified PCI reviewer, acquirer or other authorised party has confirmed the applicable SAQ and scope.
- [ ] Checkout CSP, script controls, vulnerability management and change detection have been approved.
- [ ] Payment secrets and cardholder data are excluded from logs, telemetry and session replay.
- [ ] Finance, Security and Compliance approve activation.

## Acceptance-criteria response

| Acceptance criterion | Answer |
| --- | --- |
| Web flow is selected and sandbox-proven | **Selected:** LiteAPI Payment SDK in the Blazor browser application. **Evidence still required:** complete sandbox execution including 3-D Secure, return, duplicate and recovery cases. |
| Future mobile flow is either selected or explicitly deferred | **Satisfied:** native mobile payment is deferred. Future mobile must use an official native SDK or system-browser hosted flow. |
| PCI scope and responsibilities are recorded by a qualified reviewer/provider | **Target recorded:** SAQ A or acquirer-approved equivalent is expected because cardholder-data functions are outsourced. **Final evidence still required:** current AOC, responsibility matrix and qualified confirmation. |
| Each selected production route satisfies ADR-0007 and is approved for activation | **Architecture satisfies ADR-0007 in principle:** no custom card form and no readytogo.travel handling of raw card data. **Activation remains pending:** production evidence and owner approval. |

## Recommended parent-issue answer

The following may be copied into the parent OI:

> The selected web payment route is LiteAPI's official browser Payment SDK or another LiteAPI-approved hosted component. The .NET 10 Blazor client mounts the provider component through JavaScript interop; raw card and wallet credentials are entered into and handled by the LiteAPI-managed component and its PCI-compliant underlying payment provider. readytogo.travel stores only its checkout correlation plus the LiteAPI `prebookId` and `transactionId`, and completes the booking after an idempotent HTTPS return flow. Custom card forms and unsupported web views are prohibited. Native Android/iOS payment is deferred until LiteAPI supports an approved native SDK or system-browser hosted route. Cards are supported and Google Pay is publicly documented; Apple Pay is documented for the LiteAPI-supported direct Stripe Payment Element route subject to eligibility and domain verification. Other Australian payment methods are capability-gated and must not be advertised without account evidence. The expected merchant PCI validation is SAQ A or an acquirer-approved equivalent, but production remains blocked until LiteAPI's current AOC, the payment-provider responsibility matrix, qualified PCI-scope confirmation, and sandbox/production evidence for 3-D Secure, return, abandonment and duplicate handling are approved.

## Recommended status

**Keep OI-0006 in review until the production evidence checklist is complete.**

The architecture choice is no longer open. The remaining work is compliance validation and production enablement.

Closing OI-0006 before the provider AOC and qualified scope confirmation would incorrectly treat use of the SDK as eliminating readytogo.travel's PCI DSS obligations.

## Decision impact

This answer allows:

- Blazor payment-component implementation;
- LiteAPI SDK sandbox integration;
- server-side checkout-session design;
- return and recovery workflow implementation;
- payment/booking idempotency;
- PCI-minimised logging and security controls;
- hotel implementation; and
- mobile payment deferral.

It continues to block:

- production checkout activation;
- custom card forms;
- readytogo.travel API receipt of cardholder data;
- unsupported mobile web views;
- advertising unverified Australian payment methods;
- declaring SAQ A without qualified confirmation; and
- relying on expired or non-service-specific provider compliance evidence.

## References

### LiteAPI/Nuitee Connect

- [User Payment — Nuitee SDK](https://docs.liteapi.travel/docs/user-payment)
- [Alternative — User Payment with Stripe SDK](https://docs.liteapi.travel/docs/direct-stripe-integration-stripe-elements)
- [Regulatory Compliance](https://docs.liteapi.travel/docs/regulatory-compliance)
- [Security Overview](https://docs.liteapi.travel/docs/security-privacy-compliance-overview)
- [Revenue Management and Commission](https://docs.liteapi.travel/docs/revenue-management-and-commission)

### PCI Security Standards Council

- [FAQ Clarifies New SAQ A Eligibility Criteria for E-Commerce Merchants](https://blog.pcisecuritystandards.org/faq-clarifies-new-saq-a-eligibility-criteria-for-e-commerce-merchants)
- [PCI SSC Document Library](https://www.pcisecuritystandards.org/document_library/)
- [PCI SSC Merchant Resources](https://www.pcisecuritystandards.org/merchants/)
- [PCI SSC Glossary — Attestation of Compliance](https://www.pcisecuritystandards.org/glossary/)

## Limitations

This answer is based on the public LiteAPI/Nuitee Connect documentation and PCI Security Standards Council material available on 28 July 2026.

It is an architecture and evidence assessment, not a QSA determination, legal opinion or formal PCI attestation.

The production LiteAPI account, payment SDK configuration, current AOC, underlying processor evidence, acquirer requirements and executed merchant terms were not supplied for independent review. Account-specific evidence may change the applicable payment methods, merchant responsibilities or PCI validation route.
