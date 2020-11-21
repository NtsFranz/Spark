THIS FORMAT SPEC IS NOT COMPLETE AND HAS SOME INCORRECT INFORMATION
# .milk Replay Compression Format
### File Header

| Bytes | Data |
| ----- | ---- |
| 1 | MILK version |
| 2-20 | `client_name` ASCII string |
| 1 | null byte |
| ~36 | `sessionid` ASCII string |
| 1 | null byte |
| 7-15 | `sessionip` ASCII string |
| 1 | null byte |
| (2-20)*n | ASCII JSON array of player names, where n=number of players in the match at any time|
| 4*n | Player numbers |
| 4*n | Player levels |
| 8*n | Player `userid`s |
| 1 | headerbyte (described below) |

Below is the bit flags structure for the headerbyte

| Bit number | Field name |
| ---------- | -----------|
| 7-3     | `Currently Unused Bits` |
| 2       | `Tournament Match Flag` |
| 1       | `Match Type, Arena = 0, Combat = 1, Lobby = 2`  |
| 0       | `Public or Private, Public = 0, Private = 1` |


## Frames
Compressed Milk data is made of one or more "frames". Frames contain n-chunks where n is the capture rate. Frames are captured at a lower rate than chunks, as they contain less rapidly-changing data.
Each frame is independent and supports independent decompression.

### Frame Format
Below is the structure of a single frame.

| `Header` | `Blue Points` | `Orange Points` | `Restart requests` (bitmask with blue<<0, orange<<1) | `Last Score` | `Team Stats` | `Player Stats` |
|:--------:|:-------------:|:---------------:|:---------------:|:------------:|:------------:|:--------------:|
| `0xFEFD` |     1-byte    |     1-byte      |     1-byte      |  < approx 64 bytes    |    48-bytes   |     48-bytes    |

## Chunks
Info on chunks will be written later

### Chunk Format
One chunk is one reading of data from the API

| `Header` | `Disc Position` |`Game State`| `Position Data` | `Possession` | `Blocking` | `Stunned` |
|:--------:|:---------------:|:----------:|:---------------:|:------------:|:----------:|:---------:|
| `0xFDFE` |     6-bytes     |   1 byte   |     n-bytes     |    1-byte    |   1-byte   |  1-byte   |

#### Game State
These values all have a length of 1-byte and use bit flags to determine state.
Below is the structure of these bytes
| Bit number | Field name |
| ---------- | -----------|
| 7       | `Post Sudden Death` |
| 6       | `Sudden Death`  |
| 5       | `Post Match`    |
| 4       | `Round Over`    |
| 3       | `Score`         |
| 2       | `Playing`       |
| 1       | `Round Start`   |
| 0       | `Pre-Match`     |

#### Possession, Blocking, Stunned
These values all have a length of 1-byte and use bit flags to determine state.
Below is the structure of these bytes
| Bit number | Field name |
| ---------- | -----------|
| 7-4        | `Orange Players State` |
| 3-0        | `Blue Players State`   |

Example of where Blue team's Player 3 has disc
|  `Flag_Value`  |   `0`   |  `0`  |  `1`  |  `0`  |  `0`  |  `0`  |  `0`  |  `0`  |
| -------------- | ----- | --- | --- | --- | --- | --- | --- | --- |
| `Player Index` |   `0`   |  `1`  |  `2`  |  `3`  |  `4`  |  `5`  |  `6`  |  `7`  |

The result of this would be 0x10

A bit is flagged as 1 if the field is true (ie. Player does have possession, player is stunned,etc)

## License
[MIT](https://choosealicense.com/licenses/mit/)