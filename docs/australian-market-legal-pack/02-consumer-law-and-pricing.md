---
document_type: legal-baseline
title: Australian Consumer Law and Pricing
status: implementation-baseline
reviewed: 2026-07-29
owners:
  - Legal/Compliance
  - Product
  - Finance
---

<!-- markdownlint-disable MD013 MD025 -->

# Australian Consumer Law and Pricing

## Baseline

The Australian Consumer Law applies to the Australian consumer sale and cannot be displaced by supplier terms, platform wording or a foreign governing-law clause where the law applies.

ACCC guidance states that consumer guarantees apply to travel services and apply whether the consumer books directly, through a travel agent or through a booking platform/intermediary. See [10-source-register.md](10-source-register.md), sources `ACL-01`, `ACL-02` and `ACL-07`.

## Price display requirements

### Minimum total price

Display the minimum total cost as a single figure at the earliest actionable offer stage.

Include where known and quantifiable:

- GST;
- airport, government and supplier taxes;
- unavoidable booking fees;
- platform service fees;
- mandatory resort/property fees collected through checkout;
- unavoidable payment charges;
- pre-selected extras; and
- any other unavoidable charge payable to complete the transaction.

The total price must be at least as prominent as any component price.

A price breakdown may still be shown, but it must reconcile exactly to the prominent total.

### Charges not yet quantifiable

Where a charge cannot be quantified at the time of display:

- disclose that the charge exists;
- explain who charges it;
- explain when and in which currency it is payable;
- show the calculation method or known range where available;
- do not describe the displayed total as all-inclusive; and
- repeat the disclosure before payment.

Examples can include a property charge payable locally or a government tax determined by traveller circumstances.

### Optional extras

- Default optional extras to unselected.
- Explain the effect of baggage, seats, meals, room upgrades, insurance and other additions before selection.
- Update the total immediately when an extra is selected.
- Do not use dark patterns, false scarcity or obstructive deselection.

### Currency

For an Australian point of sale:

- default to AUD;
- identify the actual charging currency;
- state when an amount is an estimate or converted display value;
- identify who performs currency conversion;
- warn that the card issuer or provider may apply its own conversion or international transaction fee where relevant;
- retain the quoted, charged and supplier-settlement currencies; and
- do not conceal a margin inside an exchange rate without approved disclosure.

### Card surcharges

A card surcharge must not exceed the applicable cost of acceptance under the Australian surcharge rules.

Where no surcharge-free payment method exists, the minimum unavoidable surcharge must be incorporated into the displayed minimum price.

Finance must retain current evidence supporting each surcharge rate and review it when provider pricing changes.

## Marketing and offer accuracy

Marketing, search results and checkout must not mislead consumers about:

- availability;
- fare or room inclusion;
- cancellation flexibility;
- refundability;
- supplier identity;
- baggage;
- taxes and local charges;
- loyalty benefits;
- transfer or connection requirements;
- visa or entry eligibility;
- accessibility;
- support;
- price expiry; or
- confirmation status.

Avoid absolute claims such as `guaranteed`, `fully refundable`, `instant refund`, `best price`, `live`, `real time` or `all fees included` unless the claim is substantiated for that product and customer.

The ACCC's Webjet proceeding is a specific warning against displaying a completed confirmation after taking payment when the flight has not actually been booked. See source `ACL-08`.

## Booking state

Use distinct states:

1. `Offer displayed`
2. `Offer selected`
3. `Repriced/verified`
4. `Payment authentication`
5. `Payment authorised or completed`
6. `Supplier booking pending`
7. `Supplier confirmed`
8. `Ticketed or fulfilment complete`, where separate
9. `Failed`
10. `Cancelled`
11. `Refund pending`
12. `Refund settled`

Only states 7 or 8 may be described to the customer as confirmed, depending on the product.

A payment success message is not a booking confirmation.

## Consumer guarantees and remedies

Terms must not state or imply:

- `no refunds under any circumstances`;
- the platform has no responsibility regardless of its own conduct;
- all consumer rights are limited to supplier policy;
- a consumer must deal only with the supplier where the operating entity has legal responsibility;
- a short contractual claim period removes statutory rights; or
- supplier compensation policies replace consumer guarantees.

Travel delays and cancellations may create rights to replacement, refund or other remedies depending on circumstances. The ACCC notes that supplier/airline compensation policies sit in addition to consumer guarantees and cannot remove them.

Where a third party prevents supply, the outcome may depend on the contract, but customer wording must not overstate that exception or misrepresent the applicable facts.

## Platform fees and refund treatment

Every fee must be classified as:

- supplier price;
- tax/government charge;
- payment surcharge;
- platform booking fee;
- platform servicing fee;
- supplier cancellation/change fee;
- fare or rate difference;
- refund processing amount; or
- other approved category.

For each category, customer terms must state:

- when the fee is earned;
- whether it is refundable;
- what occurs if the supplier rejects the booking;
- what occurs if the supplier cancels;
- what occurs if readytogo.travel causes the failure;
- what occurs if the consumer changes their mind;
- whether ACL remedies may override the contractual position; and
- whether a separate quote/acceptance is needed.

Do not describe a fee as non-refundable where doing so would mislead the consumer about non-excludable rights.

## Unfair contract terms

Standard consumer terms are presumed to be standard form unless proven otherwise. Since 9 November 2023, proposing, using or relying on unfair terms can attract penalties.

Counsel must specifically review:

- broad unilateral price-change rights;
- unilateral cancellation without equivalent customer rights;
- automatic forfeiture of all money;
- broad indemnities;
- exclusions for the platform's own negligence or breach;
- deemed acceptance of unknown future supplier terms;
- variation by website publication without notice;
- one-sided evidence clauses;
- mandatory foreign courts that create practical barriers;
- short limitation periods;
- no-chargeback clauses;
- non-disparagement clauses;
- broad account suspension;
- unrestricted data use; and
- clauses allowing the platform to keep supplier refunds.

## Complaints and dispute handling

Provide:

- a visible support channel;
- complaint acknowledgement;
- a case reference;
- reasonable status updates;
- escalation to Operations and Legal/Compliance;
- supplier escalation where needed;
- final-response reasons;
- state/territory consumer-agency information where unresolved; and
- record retention under the approved schedule.

Do not require the consumer to waive legal rights before receiving an undisputed refund.

## Product acceptance criteria

- [ ] Minimum total price is prominent at search and checkout.
- [ ] All quantifiable unavoidable charges are included.
- [ ] Optional extras are unselected by default.
- [ ] Currency and charging currency are explicit.
- [ ] Card surcharge evidence exists.
- [ ] Booking is not labelled confirmed before supplier confirmation.
- [ ] Customer rights wording preserves the ACL.
- [ ] Fee refundability is defined by failure scenario.
- [ ] Standard terms have received unfair-contract-term review.
- [ ] Complaint and refund status workflows are operational.
- [ ] Marketing claims have an evidence owner and expiry/review date.
