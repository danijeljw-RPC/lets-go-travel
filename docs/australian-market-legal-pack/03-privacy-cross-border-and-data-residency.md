---
document_type: privacy-baseline
title: Australian Privacy, Cross-Border Processing and Data Residency
status: implementation-baseline
reviewed: 2026-07-29
owners:
  - Privacy
  - Legal/Compliance
  - Security
  - Data
---

<!-- markdownlint-disable MD013 MD025 -->

# Australian Privacy, Cross-Border Processing and Data Residency

## Scope assumption

Implement the Privacy Act 1988 and Australian Privacy Principles as the baseline.

Do not design around the small-business exemption or another exemption unless Australian privacy counsel records that the exemption applies to the exact entity and all relevant activities. Exemptions can be lost or narrowed by the nature of the business, contractual commitments or particular handling activities, and the platform requires the full APP baseline regardless.

## Data controller/accountability position

The Australian operating entity determines why customer and traveller data is collected and is responsible for:

- collection notices;
- lawful and fair collection;
- data minimisation;
- supplier disclosures;
- access and correction;
- retention and destruction;
- security;
- direct marketing;
- cross-border disclosures; and
- breach response.

LiteAPI/Nuitée publicly describes the customer as controller and Nuitee Connect as processor under GDPR-like frameworks, but the executed agreement and Australian legal analysis must determine roles for each activity. See source `LITE-02`.

## Data categories

Maintain a record of processing covering at least:

- account and contact details;
- traveller names and dates of birth;
- passport/nationality/visa information;
- loyalty numbers;
- itinerary and location data;
- accommodation and flight preferences;
- payment references and non-sensitive payment metadata;
- support communications;
- IP address, device and security telemetry;
- emergency contacts;
- minor/guardian relationships;
- accessibility, dietary and health-related information;
- supplier identifiers and booking records;
- marketing preferences; and
- audit/security evidence.

## Collection and notice

At or before collection, tell the individual:

- the collecting legal entity;
- why the information is collected;
- the main consequences if it is not provided;
- the usual recipients, including suppliers and processors;
- likely overseas disclosures and countries where practicable;
- how to access and correct information;
- how to complain;
- the privacy contact; and
- any material automated decision-making information required by law.

Use contextual notices for:

- traveller details entered by another person;
- passport/document capture;
- accessibility or health requests;
- emergency contacts;
- minors;
- location/device information;
- marketing;
- analytics/session replay; and
- AI-assisted functions.

## Collection from a booking purchaser about another traveller

A purchaser may provide information about another traveller only where they confirm that:

- they are authorised to make the booking;
- the traveller has been informed where practicable;
- sensitive information is supplied with appropriate consent or another valid basis;
- the information is accurate; and
- required supplier disclosures are authorised.

Provide a traveller-facing privacy notice in booking communications where practicable.

## Sensitive information

Health, disability, accessibility and some dietary information may be sensitive information.

Collect it only where:

- it is reasonably necessary for the booking or service;
- consent has been obtained unless a valid exception applies;
- the purpose is explicit;
- access is restricted;
- free text is minimised;
- it is disclosed only to necessary suppliers; and
- retention is shorter where long-term storage is unnecessary.

Do not infer medical or disability information from behaviour or notes for unrelated profiling.

## Passport and government identifiers

Passport details are high-risk identity information.

Controls must include:

- collect only required fields;
- avoid storing document images unless a supplier or legal process requires them;
- do not use a passport number as the platform's internal account identifier;
- encrypt and restrict access;
- prevent analytics/session replay capture;
- record every disclosure;
- delete under the approved retention schedule; and
- provide a manual correction process.

## APP 8 cross-border disclosure

OAIC guidance states that before an APP entity discloses personal information to an overseas recipient, it generally must take reasonable steps to ensure the recipient does not breach the APPs, and the Australian entity can remain accountable for the overseas recipient's handling.

Required controls:

- identify each overseas recipient and country;
- identify the data and purpose;
- conduct supplier due diligence;
- execute privacy and security terms;
- require purpose limitation and confidentiality;
- restrict subprocessors;
- require security controls and incident notice;
- require assistance with access, correction, deletion and complaints;
- set retention and deletion obligations;
- control onward transfers;
- maintain audit/assurance rights;
- record any relied-upon APP 8 exception; and
- describe likely countries in the privacy policy and collection notices where practicable.

Consent to overseas disclosure must not be used casually to avoid accountability. Any APP 8 consent pathway requires specific legal review and clear explanation of the consequence.

## Data residency decision

### Legal baseline

No general requirement has been identified in the Privacy Act that all ordinary private-sector travel data must remain physically stored in Australia.

The legal issue is usually cross-border disclosure, security, transparency, accountability, contractual restrictions and sector/customer requirements rather than a blanket localisation rule.

### Platform policy

For the Australian market:

- use an Australian primary application/data region where commercially and technically reasonable;
- keep platform configuration, canonical bookings, financial records and audit data in the approved Australian region by default;
- document every replicated, supported or processed overseas location;
- limit overseas data to what the supplier or service requires;
- encrypt data in transit and at rest;
- use separate production/non-production environments;
- prohibit production personal data in developer tooling unless approved; and
- maintain a country/subprocessor register.

Australian hosting does not remove APP 8 obligations when data is sent to LiteAPI, airlines, hotels, GDS/NDC/LCC providers or overseas support/subprocessors.

## LiteAPI/Nuitée position

LiteAPI publicly states that:

- it processes booking, contact and technical data necessary for the service;
- customers are responsible for lawful collection and sharing;
- it may process data outside the customer's country, including in the European Union;
- it uses safeguards for international transfers;
- it offers a DPA;
- subprocessors can be provided on request; and
- personal data is deleted or anonymised when no longer required.

Production approval requires:

- executed DPA;
- subprocessor list;
- hosting and support countries;
- Australian APP 8 terms;
- incident-notice timing;
- access/deletion assistance;
- retention and termination deletion; and
- controller/processor allocation for payments, fraud, support and bookings.

## Privacy policy

The privacy policy must include:

- identity/contact details;
- categories of information;
- collection methods;
- purposes;
- disclosures;
- overseas recipients and countries where practicable;
- cookies/analytics;
- direct marketing and opt-out;
- access and correction;
- complaints;
- retention/destruction;
- security summary;
- minors;
- automated decisions where applicable;
- changes/effective date; and
- links to contextual collection notices.

OAIC guidance requires likely overseas disclosures and countries to be described where practicable. See source `PRIV-03`.

## Direct marketing

- Separate booking/service communications from marketing.
- Record consent or another approved basis.
- Provide a functional unsubscribe.
- Do not use sensitive information for direct marketing without specific approval.
- Do not market directly to children.
- Do not infer travel vulnerability or health status for advertising.
- Ensure supplier co-marketing and audience sharing are separately approved.

## Children's Online Privacy Code

The OAIC is required to finalise and register the Children's Online Privacy Code by 10 December 2026. The Code is intended to apply to specified online services likely to be accessed by children or concerned with children's activities.

As at 29 July 2026, the final registered Code is not yet available.

Required action:

- perform an applicability assessment before 10 December 2026 and before production launch if later;
- compare the final Code with account, booking, analytics, consent, notice, profiling and retention flows;
- adopt child-safe defaults even where scope is uncertain;
- provide age-appropriate notices;
- avoid manipulative consent;
- minimise age assurance data;
- do not create direct minor accounts at launch; and
- record guardian authority for minor booking data.

The exposure draft is not treated as final law, but it is a design-warning source.

## Automated decisions

From 10 December 2026, additional privacy-policy transparency obligations may apply where computer programs use personal information to make decisions that could reasonably be expected to significantly affect an individual's rights or interests.

Before deploying AI or rules that materially affect booking access, fraud outcomes, support priority, refunds or account restrictions:

- document the decision;
- identify personal information used;
- provide human review;
- test bias and error;
- explain the outcome where appropriate;
- allow correction/challenge;
- update the privacy policy; and
- obtain Legal/Compliance approval.

## Data subject operations

Implement:

- identity verification proportionate to the request;
- access;
- correction;
- deletion/destruction where required;
- consent withdrawal;
- marketing opt-out;
- complaint handling;
- supplier/subprocessor propagation; and
- decision/audit records.

Do not expose another traveller's information merely because the requester paid for the booking.

## Acceptance criteria

- [ ] Privacy Act scope position approved.
- [ ] Record of processing complete.
- [ ] APP 5 notices implemented.
- [ ] Privacy policy lists overseas disclosures/countries where practicable.
- [ ] APP 8 vendor controls approved.
- [ ] LiteAPI DPA and subprocessors reviewed.
- [ ] Australian primary-region policy implemented or exception approved.
- [ ] Sensitive and passport data controls tested.
- [ ] Traveller data supplied by another person is addressed.
- [ ] Direct marketing consent/opt-out implemented.
- [ ] Children's Online Privacy Code review scheduled before 10 December 2026.
- [ ] Automated decision transparency reviewed.
- [ ] Access, correction and deletion workflows tested.
