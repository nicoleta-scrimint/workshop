# Message ID

## Distributed time

In the previous lecture we talked about total order of events in the system. We noticed that out-of-order delivery is inevitable unless all components of a system agree on a single log technology in which case that log defines the total order of events. We've discussed the downsides of that approach, especially with regards to coupling. Lastly, we decided to focus on the class of systems where the top-level components are fully autonomous which means that each of them defines its own order of events.

## Idempotent business logic

Moving from pure mathematics to more practical terms, a business logic operation is idempotent if, based only on the state of the data, it can detect if it has been already applied and if so, skip doing any modifications. This condition can be easily satisfied if we guarantee order of operations. For example an operation to add an item of a given type to an order can be made idempotent if it includes a check if an item of that type has already been added. Similarly, an operation to remove an item can be made idempotent if it includes a check if an item of that type exists. Both operations, when used separately, produce correct behavior even if they are duplicated but when the duplicates are interleaved -- the result is incorrect. Why is that? The reason is that idempotence of a business operation depends on the state of the data. To ensure correct results the duplicates need to see the data in exactly the state that has been produced by the processing of the fist copy of a given message. When re-ordering is allowed, the state of the data is altered.

## Set algebra

The last exercise has shown that in a system like that we need a stronger guarantee than idempotence of each data access operation to ensure correctness. We can show the same behavior using the set algebra. The `union` and `intersection` operations are idempotent. Given sets `A` and `B`, `A + B` is equal to `(A + B) + B`. Similarly, `A - B` is equal to `(A - B) - B`. Combining both operations without re-ordering also yields correct results: `(A + B) - B` is equal to `(((A + B) + B) - B) - B` and is equal `A`. Now if we re-order our duplicates we can end up with `(((A + B) - B) - B) + B` which results `A + B`.

## Message ID

So far we established that the business logic idempotent behavior cannot be based on the state of the data. What else can we use? Here the concept of message unique ID comes to the rescue. The message ID is a property of a message assigned by the sender before passing the message to the messaging infrastructure. If a duplicates are introduced along the way, all copies of a given message carry the same ID. The receiver can use the ID to distinguish messages that have merely identical content from true duplicates.

## Natural vs artificial

The message ID can be either natural or artificial. A natural ID is part of the business domain. For example, if we are modeling a shooting range domain, the `Shoot` message can be uniquely identified by the name of the person who's shooting and the number of the attempt e.g. `Shoot(Szymon, 3)` means that the message represents my third attempt to hit the target. 

The artificial ID is not part of the business domain. Usually a Guid is used to ensure global uniqueness in such case. The Guid is generated when sending a message. Although the natural ID concept looks interesting, in most cases the artificial ID generation is better as it allows better separation of business concerns from infrastructure concerns.

## Using message ID for detecting duplicates

How can we use the message ID for detecting duplicates? If we are fortunate to use a good old relational database we can add a table that store the IDs of messages we already processed. Prior to executing any message handling logic we query that new table (let's call it `ProcessedMessages`) and check if it contains a row with ID equal to the ID of the message we are about to process. If so then that message is a duplicate and we can simply drop it. Note: remember that even if we drop a message as a duplicate, we need to re-try publishing outgoing messages. We discussed that already.

## Marking a message as processed

To be able to mark a message as processed (and therefore prevent processing duplicates of that message in future) each message handling transaction need to be extended with an `INSERT` statement to that table:

```c#
begin tran

-- execute SQL statements resulting from the business logic execution
insert into ProcessedMessages (ID) values (`abc1234`)
commit tran
```

There are two important things about the code above. First, making the `ID` column a *primary key* of the `ProcessedMessages` column ensures that adding a second row with the same ID fails. The second important thing is that business logic changes and marking a message as processed needs to happen in the same transaction. Why? Try thinking about anomalies that could happen if these two were not part of the same transaction. 

An obvious example is a failure just before marking a message as processed. In such case, provided there is no transaction that spans both operations, the business logic processing results would be persisted while the message would not be marked as processed. The message processing would be retried immediately causing the business logic to be applied once again.
