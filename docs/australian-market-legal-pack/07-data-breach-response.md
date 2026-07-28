---
document_type: incident-response-baseline
title: Australian Data Breach and Cyber Incident Response
status: implementation-baseline
reviewed: 2026-07-29
owners:
  - Security
  - Privacy
  - Legal/Compliance
  - Operations
---

<!-- markdownlint-disable MD013 MD025 -->

# Australian Data Breach and Cyber Incident Response

## Legal baseline

The Privacy Act Notifiable Data Breaches scheme requires covered entities to assess suspected eligible data breaches and notify the OAIC and affected individuals when the statutory criteria are met.

OAIC guidance states:

- assessment must be reasonable and expeditious;
- reasonable steps must be taken to complete the assessment within 30 days;
- 30 days should be treated as a maximum, not a default target; and
- after forming a reasonable belief that an eligible data breach occurred, the OAIC and affected individuals must be notified as soon as practicable.

See sources `BREACH-01` to `BREACH-04`.

## Internal response objectives

- protect people;
- contain the incident;
- preserve evidence;
- understand affected data and systems;
- restore secure service;
- satisfy legal/contractual notification;
- coordinate suppliers;
- communicate accurately;
- prevent recurrence; and
- maintain an auditable decision record.

## Severity

Treat the following as presumptively high severity until assessed:

- passport or identity-document data;
- cardholder data or payment credentials;
- authentication tokens/API keys;
- detailed traveller itineraries and location;
- guardian/minor data;
- health/accessibility information;
- account takeover;
- mass booking/customer data;
- supplier credentials;
- cross-customer access;
- ransomware;
- extortion;
- privileged support access; and
- production database extraction.

## Response phases

### 1. Detect and contain

- open an incident record;
- appoint Incident Commander;
- restrict access/revoke credentials;
- preserve logs and forensic evidence;
- stop ongoing disclosure;
- isolate affected systems;
- prevent destructive automated cleanup;
- engage supplier/provider incident channels;
- preserve business continuity; and
- record exact times in UTC and Australia/Sydney.

### 2. Triage and assess

Identify:

- incident cause;
- systems;
- data classes;
- number and type of people;
- whether data was accessed, disclosed, lost, altered or unavailable;
- encryption and key exposure;
- likely recipient;
- ability to misuse data;
- minors/vulnerable travellers;
- overseas recipients;
- payment/identity fraud risk;
- travel/safety risk;
- remedial action;
- contractual notification deadlines; and
- potential serious harm.

### 3. Notify and communicate

Legal/Privacy determines notification.

Potential recipients include:

- OAIC;
- affected individuals;
- LiteAPI/Nuitée;
- airlines/hotels/providers;
- payment processor/acquirer;
- PCI forensic/investigation contacts where applicable;
- cyber insurer;
- law enforcement;
- ACSC/ReportCyber;
- directors/board;
- customers;
- employees/contractors; and
- other regulators required by the exact incident.

Do not notify publicly before facts and safety consequences are assessed, except where urgent warning is necessary.

### 4. Recover and review

- eradicate root cause;
- rotate secrets;
- patch;
- restore from trusted sources;
- reconcile bookings/payments;
- monitor misuse;
- support affected individuals;
- complete post-incident review;
- update threat model and controls;
- verify supplier remediation;
- retain required evidence; and
- close only with owner approval.

## NDB assessment record

Record:

- date/time grounds to suspect arose;
- assessing entity/entities;
- information involved;
- circumstances;
- remedial actions;
- serious-harm factors;
- likely affected individuals;
- reasons for eligibility/non-eligibility decision;
- legal advice;
- OAIC statement;
- individual notification method;
- notification date;
- exceptions relied upon; and
- executive approval.

If assessment exceeds 30 days, record why and every reasonable step taken, while escalating to Legal/Compliance.

## Eligible data breach test

An eligible data breach generally involves unauthorised access to, unauthorised disclosure of, or loss of personal information in circumstances likely to result in serious harm, where remedial action does not prevent the likely serious harm.

Counsel/Privacy applies the statutory test to the facts.

## Remedial action

Prompt remedial action can affect whether notification is required.

Examples:

- confirmed deletion by unintended recipient;
- credential revocation before access;
- remote wipe;
- effective encryption with uncompromised keys;
- cancellation/reissue of exposed credentials;
- blocking fraudulent booking/payment action; and
- notifying a traveller of a safety risk.

Do not assume remediation succeeds without evidence.

## Supplier and cross-border incidents

Contracts must require suppliers to:

- notify rapidly after discovery;
- provide facts and updates;
- preserve evidence;
- identify affected data;
- identify subprocessors/countries;
- support NDB assessment;
- avoid contacting Australian customers without coordination unless legally required;
- provide remediation;
- provide final root-cause analysis; and
- support access/deletion/correction consequences.

Where multiple entities may have notification duties, record who assesses, who notifies and how duplication/inconsistency is avoided.

An overseas supplier incident can still create Australian APP 8 accountability.

## Customer notification content

Where required, provide:

- identity/contact details of notifying entity;
- description of breach;
- kinds of information;
- recommended steps;
- specific fraud/travel safety actions;
- support contact;
- updates;
- scam warning; and
- accessible/translated formats where needed.

Avoid speculative reassurance.

## Operational internal targets

These are internal controls, not statements of statutory deadlines:

| Action | Internal target |
| --- | --- |
| Security acknowledgement | 15 minutes for critical alert |
| Incident Commander appointed | 30 minutes |
| Legal/Privacy escalation | 1 hour for likely personal-data incident |
| Supplier notice | Within contract deadline; target 2 hours for critical incident |
| Preliminary impact brief | 4 hours |
| Executive/board brief | 4 hours for critical incident |
| NDB assessment cadence | Daily until decision |
| Individual/OAIC notification | As soon as practicable after eligibility decision |

## Exercises

Run at least annually and after material architecture change:

- LiteAPI compromise;
- API key leak;
- cross-customer booking exposure;
- passport data exposure;
- payment-page script compromise;
- ransomware;
- malicious support user;
- lost admin device;
- supplier webhook spoofing;
- child itinerary exposure; and
- data sent to wrong supplier/customer.

## Acceptance criteria

- [ ] Response plan approved.
- [ ] Incident roles and contacts populated.
- [ ] 24/7 escalation exists for production.
- [ ] NDB assessment template implemented.
- [ ] Supplier notice clauses approved.
- [ ] OAIC/customer notification templates prepared.
- [ ] Evidence preservation procedure implemented.
- [ ] Cyber insurer/payment contacts recorded.
- [ ] High-risk data classes tagged.
- [ ] Exercise completed and actions closed.
