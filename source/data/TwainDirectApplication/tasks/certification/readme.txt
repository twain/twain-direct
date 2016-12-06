The tests are designed to support as much automation as possible.  As such they're designed with a test header, which offers information about the test along with the expected result.

The text beginning on the line immediately following ***DATADATADATA*** is used in the test.

{
    "category": "JSON Syntax"
    "summary": "name of the test",
    "description": "long description",
    "expectedErrorCode": "invalidJson",
    "expectedCharacterOffset": 10
    "expectedJsonKey": "actions[0].streams[0]..."
}
***DATADATADATA***
{
	"badboolean": treu
}