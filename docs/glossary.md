# Glossary

| Term | Meaning |
| --- | --- |
| Booking | The platform's durable representation of a supplier reservation, including current state and history. |
| Canonical snapshot | A deterministic supplier-neutral representation of meaningful booking state used for version comparison. |
| Customer | The authenticated account holder who owns trips and bookings. |
| Fulfilment data | Traveller and reservation information disclosed to a supplier to search, book, service or cancel travel. |
| LiteAPI/Nuitee Connect | The initial supplier under evaluation. The two names appear in current vendor documentation. |
| Operational flight status | Live or near-live gate, terminal, delay, aircraft, diversion and actual movement information. It is separate from booking state. |
| Platform | The `readytogo.travel` application services, data and client-facing API. |
| `ReadyToGoTravel` | The default root namespace or package prefix for all product code. Apply normal ecosystem casing rules when lowercase package names are required. |
| `RTGT` | The product shortcode for `readytogo.travel`. |
| Reconciliation | Retrieval and comparison of supplier booking state against the platform's current booking state. |
| Supplier | An external inventory or fulfilment provider. |
| Traveller | A person included in a trip or booking. A traveller may differ from the customer. |
| Trip | The customer-facing container for bookings, manual itinerary items, documents, reminders and travel context. |
| Version | An immutable record created when a meaningful canonical booking change is detected. |
