# Bookings Sample Application

This projects shows some of the features of Eventuous

//TODO
Describe features used

## Usage

Run both `Bookings` and `Bookings.Payments` projects and then open `http://localhost:5051/swagger/index.html` and `http://localhost:5000/swagger/index.html` respectively. 
Here you can use SwaggerUI to initiate commands and queries that will result in events being raised.

### Example commands

Bookings -> BookRoom (`/bookings/book`)

This command raises and event which is stored in the database but also in Mongo via a registered projection

Bookings.Payments -> RecordPayment (`/recordPayment`)

This command raises multiple events which is stored in the database but also puts a message in RabbitMQ which is transformed from a domain event into an integration event. Upon the integration event being handled logic and other domain events are triggered resulting
in more database entries and Mongo projection data being stored.

Bookings.Payments -> `CommandService:RecordPayment` -> `PaymentRecorded` -> `IntegrationSubscription` -> `BookingPaymentRecorded` -> Bookings -> RabbitMQSubscription -> `PaymentsIntegrationHandler` -> `BookingsCommandService:OnExisting<RecordPayment>` -> `V1.PaymentRecorded` -> `BookingState:On<V1.PaymentRecorded>`-> `V1.BookingFullyPaid` -> `BookingStateProjection:On<V1.PaymentRecorded>` -> `BookingStateProjection:On<V1.BookingFullyPaid>`