---
document_type: legal-baseline
title: Travel Selling, Accreditation, Ticketing and Financial-Services Perimeter
status: implementation-baseline
reviewed: 2026-07-29
owners:
  - Legal/Compliance
  - Product
  - Supplier Integration
---

<!-- markdownlint-disable MD013 MD025 -->

# Travel Selling, Accreditation, Ticketing and Financial-Services Perimeter

## General travel-agent licensing position

Consumer Affairs Victoria states that a travel agent no longer needs a travel-agent licence, while remaining subject to laws including the Australian Consumer Law. The former national/state travel-agent licensing and compensation model was dismantled in 2014.

The project may use the following baseline:

> No general Australian travel-agent licence is assumed for the proposed online intermediary model, subject to counsel confirming the exact operating entity, every state/territory in which it carries on business, and any product-specific licensing or permit.

This baseline is not authority to ignore:

- ACL obligations;
- financial-services licensing;
- payment/remittance rules;
- direct tour/operator permits;
- airline ticketing authority;
- sanctions;
- privacy;
- accessibility/discrimination laws;
- local accommodation/tourism requirements; or
- supplier accreditation requirements.

## ATAS/ATIA accreditation

ATAS is a voluntary industry accreditation scheme operated by the Australian Travel Industry Association.

Product and Legal/Compliance must decide whether to seek accreditation based on:

- customer trust;
- supplier requirements;
- tender/investor requirements;
- complaint resolution;
- financial and professional standards;
- audit/compliance burden;
- insurance requirements; and
- marketing value.

Do not display ATAS/ATIA accreditation marks until accreditation is granted and current.

Accreditation must not be represented as a government guarantee or insolvency compensation scheme.

## Agency, intermediary and principal status

Determine status for each product and payment route.

| Role | Required customer disclosure |
| --- | --- |
| Agent/intermediary | State that the platform arranges the supplier service, identify the supplier, explain which terms apply, and describe what the intermediary does and does not control. |
| Merchant of record | Identify who charges the customer, appears on the card statement, funds refunds, handles disputes and issues receipts. |
| Principal/reseller | State that the operating entity contracts to provide the service, bears the approved obligations and may procure components from suppliers. |
| Marketplace/platform | Identify the contracting supplier and platform services; avoid implying that platform status removes ACL responsibility for platform conduct. |

Do not use one generic role statement where hotels, flights and other services use different commercial models.

## Flight ticketing

Search and booking API access does not automatically establish direct ticket-issuing authority.

Before describing the platform as issuing airline tickets, record:

- whether LiteAPI or its provider issues the ticket;
- IATA/accreditation or consolidator relationship;
- ticketing entity;
- validating carrier;
- ticket number and issuance status;
- ticketing deadline;
- void/refund/exchange authority;
- BSP/settlement role;
- customer receipt and tax treatment; and
- insolvency/chargeback allocation.

Use `booking confirmed` and `ticket issued` as separate states where applicable.

## Travel insurance

Travel insurance is a financial product.

ASIC RG 36 gives a travel-agency example and states that assisting a customer to obtain travel insurance and collecting/transmitting the premium is highly likely to constitute arranging.

Launch rule:

- do not recommend, compare, arrange, issue, bind, collect premium for or receive commission from travel insurance unless the legal authorisation model is approved;
- do not use pre-selected insurance;
- do not describe general travel information as personal financial product advice;
- do not receive insurance commission without satisfying all applicable disclosure/consent obligations;
- preserve financial-services records separately; and
- ensure staff, scripts and UI remain within the approved authorisation.

Allowed MVP alternative:

- provide a neutral reminder that customers should consider appropriate insurance;
- link to an unrelated external source only if Legal/Compliance confirms the link and commercial arrangement do not constitute arranging, advice or referral requiring authorisation; and
- receive no commission unless approved.

## Payment and merchant services

Using LiteAPI's SDK does not itself require readytogo.travel to be an AFSL holder where readytogo.travel is merely purchasing/arranging travel and is not providing a financial service. However, any broader payment feature requires separate analysis.

Review before implementing:

- customer wallet or stored value;
- holding balances for future bookings;
- remittance or money transfer;
- foreign exchange service or margin;
- credit;
- instalments;
- buy now, pay later;
- virtual cards issued to customers;
- chargeback management as a service;
- payment accounts; or
- tokenised/digital assets.

## Packages and bundled travel

Australia does not have a single EU-style package-travel regime identified in the reviewed sources, but ACL, contract, agency, negligence and insolvency exposure still apply.

Where readytogo.travel combines multiple travel components into one customer offer:

- identify whether it acts as principal for the package or agent for separate suppliers;
- show each supplier and material terms;
- define package price and allocation;
- define responsibility where one component fails;
- avoid misleading `one booking` or `fully protected` claims;
- define cancellation dependencies;
- define refunds by component;
- assess whether a new merchant/funds-flow model is created; and
- obtain specific counsel approval before selling dynamically bundled packages as a principal.

## Direct operation and permits

If the business later directly operates tours, transport, accommodation, events or activities, obtain a separate permit and liability review.

Potential triggers include:

- passenger transport;
- park and protected-area commercial tours;
- marine activities;
- adventure activities;
- liquor;
- working-with-children requirements;
- accommodation registration;
- events;
- employment/guide licensing;
- local planning; and
- public liability insurance.

This pack covers an online intermediary, not direct physical operation.

## Sanctions and destination controls

Supplier availability does not override Australian sanctions or destination restrictions.

Before launching destinations or payees with elevated risk:

- screen suppliers/payees as required;
- restrict sanctioned transactions;
- keep legal update procedures;
- avoid representing government travel advice as a legal prohibition unless it is one;
- link to current Smartraveller advice;
- warn that visas, entry and health requirements remain traveller responsibilities subject to any platform promise; and
- provide an emergency support boundary.

## Acceptance criteria

- [ ] Operating states/territories reviewed.
- [ ] Agent/principal/MOR role recorded per product.
- [ ] ATAS decision recorded.
- [ ] Flight ticketing entity and authority recorded.
- [ ] Travel insurance disabled or authorised.
- [ ] No unapproved wallet, remittance, FX or credit feature exists.
- [ ] Package/bundle role approved.
- [ ] Direct-operation activities are outside scope or separately permitted.
- [ ] Supplier and customer terms match the approved role.
