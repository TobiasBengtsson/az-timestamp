# Timestamp Service

A simple and elegant Azure-based service for timestamping anything.

The timestamp service is now solely based on Azure Functions, Azure Table Storage and Azure Queue Storage. It is thus very easy to deploy in a production environment and is usually cheap if used with a consumption-based plan.

The service maintains a hash chain of timestamps, and the tip of the chain is published to OriginStamp (and by extension, to the Bitcoin blockchain) once a day.

## Build & Deploy

Build using VS2017 and deploy easily using the Publish functionality to an Azure Function app of your choice. Please find the settings that must be configured in the Azure environment in the `local.settings.json` file in `src\TimestampService.Functions`.

For reference, a setting such as:

```
{
    "Foo": {
        "Bar": "xyz"
    }
}
```

should be configured in Application Settings as having a key of `Foo:Bar` and a value of `xyz`.

You need to have SendGrid and OriginStamp API keys in order to use this service out of the box.

Also, a storage account needs to be connected to the Azure Function app (Azure sometimes does this in the background when the Function App is created). In this storage account an Azure Table named `timestamp` and an Azure Queue named `timestamp-incoming` need to be created.

## Operation

### Adding a timestamp

To timestamp something, just post a message to the `timestamp-incoming` queue.

The format of the message should just be plain text (not JSON) of the hash that should be timestamped in a hexadecimal representation. Any even number of characters in the range a-f, A-F, 0-9 are allowed.

If the hash has already been added before, it will not be added again to the hash chain.

### Internal operation after adding

When a message is posted, the `AddTimestamp` function will process the message immediately and add the hash to a queue that is `WaitingForProcessing`. Once per hour the function `ProcessNewlyAddedHashes` is run, which adds the newly added hash(es) to the hash chain. This whole mechanism is done in two functions in order to get the total order of the hashes which is needed in the chain.

The chain is constructed by concatenating the previous chain hash with the hash to be included as the next part in the chain, and hashing the result using SHA256 in order to arrive at the next chain hash. The absolutely first hash in the chain is simply concatenated with the empty string since there is no previous chain hash to use.

Once per day, the function `PublishTipOfHashChain` is run. This function publishes the tip of the chain to OriginStamp (through their v3 API) and also send an email (through SendGrid) to the email address that has been configured in Application Settings. This email contains the complete set of information needed to connect all the timestamps processed during the day to the timestamp published to OriginStamp.

### Validate added timestamps

To get the information needed for validating a previously timestamped hash, send a HTTP GET request to `{hash}/validationChain` (handeled by the `GetValidationChain` function). If there has been a publish to OriginStamp after this hash was processed, the full hash chain from this hash until the published hash will be returned.