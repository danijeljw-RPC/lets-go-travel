---
document_type: legal-product-baseline
title: Minor Traveller and Guardian Controls
status: implementation-baseline
reviewed: 2026-07-29
owners:
  - Legal/Compliance
  - Product
  - Privacy
  - Operations
---

<!-- markdownlint-disable MD013 MD025 -->

# Minor Traveller and Guardian Controls

## Launch position

The platform does not permit a person under 18 to create the purchasing account or accept the booking contract.

An adult may book for a minor where the adult declares that they:

- are the minor's parent, guardian or otherwise authorised person;
- are authorised to provide the minor's information;
- have reviewed the supplier's minor-travel rules;
- will obtain required passports, visas, consent letters and court approvals;
- will provide accurate guardian and emergency details; and
- understand that supplier approval is required for unaccompanied or child-only travel.

This is a conservative product rule. It does not determine whether a particular minor contract is enforceable under every state or territory law.

## Booking categories

| Category | Launch treatment |
| --- | --- |
| Minor travelling with booking adult | Allowed where supplier age/occupancy rules accept the itinerary. |
| Minor travelling with another adult | Allowed only with purchaser authority declaration and supplier-required guardian documentation. |
| Minor travelling alone | Disabled by default; enable only through a controlled manual/capability-gated flow. |
| Multiple minors without an adult | Disabled by default. |
| Minor accommodation check-in without adult | Disabled unless the property expressly confirms acceptance and Operations records it. |
| Infant | Allowed only where carrier/property inventory, ticketing, occupancy and document requirements are supported. |
| Child with accessibility/medical needs | Manual review where special assistance or sensitive information is required. |

## Unaccompanied-minor capability

Approval must be carrier/provider/route-specific and record:

- minimum and maximum age;
- whether the service is mandatory or optional;
- direct versus connecting flight rules;
- domestic versus international rules;
- codeshare and operating-carrier rules;
- booking channel availability;
- required fee;
- required forms;
- check-in and collection person details;
- transfer restrictions;
- disruption handling;
- refund/change handling;
- support contacts; and
- evidence date.

Search availability alone is not evidence that an unaccompanied-minor booking can be completed or serviced.

## Parental responsibility and international travel

The Australian Passport Office states that full parental consent generally means every person with parental responsibility agrees to the issue of a child's passport, subject to court-order and special-circumstance pathways.

The platform:

- does not issue passports;
- does not decide who has parental responsibility;
- does not override court orders;
- does not state that a booking proves permission to travel;
- does not provide family-law advice;
- may require an adult declaration and supplier documentation;
- directs disputed-consent cases to independent legal advice; and
- can refuse or pause a booking where authority is uncertain.

Australian Passport Office guidance warns that taking a child overseas without the other parent's consent may be a criminal offence in some circumstances. See sources `MINOR-01` and `MINOR-02`.

## Required booking data

Collect only what is necessary:

- minor's legal name;
- date of birth;
- nationality/passport data where required;
- travelling adult;
- purchaser;
- parent/guardian contact;
- emergency contact;
- relationship declaration;
- supplier-required unaccompanied-minor details;
- assistance request; and
- document-status checklist.

Do not routinely collect:

- custody orders;
- family-court pleadings;
- detailed family disputes;
- school information;
- unnecessary health history;
- identity documents for every guardian; or
- broad proof of parentage.

Where exceptional evidence is needed, use a restricted manual case and defined retention.

## Privacy

- Treat children's data as high risk.
- Use age-appropriate explanations where the minor interacts with the service.
- Do not create child marketing profiles.
- Do not use minor itinerary/location data for advertising.
- Do not disclose guardian contact details beyond operational need.
- Restrict staff access.
- Avoid session replay on traveller-document screens.
- Apply the approved retention schedule.
- Include child data in breach harm assessment.
- Review the final Children's Online Privacy Code before 10 December 2026 and update the design if it applies.

## Sensitive information and assistance

Where health, disability, dietary or assistance information is needed:

- explain the exact supplier/service purpose;
- obtain appropriate consent;
- collect structured minimum fields;
- avoid diagnosis details unless required;
- disclose only to the necessary provider;
- warn that supplier approval is not guaranteed;
- provide an Operations escalation; and
- delete raw notes when no longer required.

## Customer terms

The minor-traveller clause must state:

- purchaser must be 18 or older;
- purchaser warrants authority;
- supplier age rules apply;
- passports, visas and consent documents remain the traveller/guardian responsibility unless the platform expressly undertakes otherwise;
- carrier/property acceptance may be required;
- unaccompanied-minor services are not guaranteed;
- the platform may pause or cancel an unsupported booking before confirmation;
- refunds follow supplier terms and ACL rights;
- family-law disputes require independent advice; and
- emergency/disruption handling may require direct supplier contact.

Avoid a clause that attempts to exclude responsibility for the platform's own misleading statement or failure to follow the approved process.

## Operations procedure

For an exception case:

1. verify the purchaser is an adult;
2. classify the minor travel type;
3. obtain supplier rules in writing or from a dated authoritative source;
4. identify operating carrier/property;
5. collect minimum guardian/emergency details;
6. explain fees and restrictions;
7. obtain purchaser acceptance;
8. complete supplier booking/manual action;
9. retain supplier confirmation;
10. provide guardian instructions;
11. set disruption escalation;
12. re-verify before departure where required; and
13. delete exceptional documents under the approved schedule.

## Acceptance criteria

- [ ] Purchasing accounts require age 18+.
- [ ] Guardian/authority declaration implemented.
- [ ] Minor travel type captured.
- [ ] Unaccompanied-minor inventory disabled by default.
- [ ] Supplier-specific capability matrix exists before enablement.
- [ ] Child privacy notice and restricted access implemented.
- [ ] Sensitive assistance workflow approved.
- [ ] Customer terms approved.
- [ ] Operations exception procedure tested.
- [ ] Children's Online Privacy Code review scheduled.
