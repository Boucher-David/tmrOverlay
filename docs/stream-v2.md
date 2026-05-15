# Stream Chat V2 Notes

This file parks the richer stream-chat work that should become a v1.x or v2 pass. The current branch should keep the v1 Stream Chat overlay focused on necessary parity improvements: Design V2 shell, fixed-height chat history, row wrapping, Twitch author color, visible badges, inline emotes, alerts as rows, native/localhost parity, and browser review replay coverage.

The deeper model below is intentionally not the required v1 finish line. It exists so we do not lose what Twitch and Streamlabs can expose.

## Current V1 Carry-Forward

- Stream Chat uses the normal Design V2 overlay shell/title.
- No invented footer is shown unless it is part of shared overlay chrome settings.
- The overlay height stays fixed; new messages push older messages up.
- Long messages wrap inside the row and increase that row height.
- Twitch rows can show author color, badges, metadata chips, and inline Twitch emotes.
- Twitch-specific content toggles stay scoped to Twitch until Streamlabs payloads are verified.
- Browser review can use a fixture URL such as:

```text
http://127.0.0.1:5187/overlays/stream-chat?streamChatFixture=all
```

## V2 Product Boundary

Stream Chat V2 should decide how to present rich stream events instead of only drawing generic chat rows.

Likely V2 features:

- Reply preview rows using parent message/user data.
- Alert-specific row treatments for resub, sub gift, raid, announcement, charity donation, bits badge tier, and watch streak.
- Bits/cheer styling separate from normal message metadata.
- Shared-chat/source-channel indication if Twitch source tags are present.
- Badge and emote image caching for browser and native.
- Platform-specific rendering paths:
  - Twitch structured chat rows.
  - Streamlabs opaque widget mode.
  - Streamlabs authenticated event feed mode.

## Twitch Data Surfaces

Twitch exposes rich chat row data through IRC tags when the client requests `twitch.tv/tags` and `twitch.tv/commands`. `PRIVMSG` covers ordinary chat, and `USERNOTICE` covers chat-visible events such as resubs and raids. Twitch also exposes EventSub types such as `channel.chat.notification`, which provides structured notice data for events that appear in chat.

Useful Twitch IRC tags for normal messages:

- `id`: message id.
- `room-id`: broadcaster/channel id.
- `user-id`: sender user id.
- `display-name`: sender display name.
- `color`: sender chosen name color.
- `badges`: badge id/version list.
- `badge-info`: badge metadata such as subscriber month count or bits tier.
- `emotes`: emote id and text ranges.
- `first-msg`: first message signal.
- `returning-chatter`: returning chatter signal.
- `bits`: bits amount for cheers.
- `mod`, `subscriber`, `turbo`, `user-type`, `vip`: role-ish flags.
- `tmi-sent-ts`: Twitch timestamp in Unix milliseconds.
- `reply-parent-msg-id`, `reply-parent-user-id`, `reply-parent-user-login`, `reply-parent-display-name`, `reply-parent-msg-body`: parent reply details.
- `reply-thread-parent-msg-id`, `reply-thread-parent-user-login`: thread root details.

Useful Twitch IRC tags for notices:

- `msg-id`: notice type, for example `resub` or `raid`.
- `system-msg`: Twitch-generated display text.
- `msg-param-*`: notice-specific data, such as sub months, sub plan, raider name, or viewer count.

Useful EventSub `channel.chat.notification` fields:

- `notice_type`: semantic event type.
- `message`: user-entered message fragments where present.
- `chatter_user_*` and `broadcaster_user_*`: actor/channel identities.
- Notice-specific objects, such as `resub`, `raid`, `sub`, `sub_gift`, `community_sub_gift`, `announcement`, `charity_donation`, and shared-chat equivalents.

Twitch does not appear to send final row CSS. It sends semantic hooks that we can style ourselves: author color, badges, roles, emote ranges, bits amount, notice type, system message, and reply context.

## Proposed Twitch V2 Model

```json
{
  "provider": "twitch",
  "transport": "irc",
  "command": "PRIVMSG",
  "id": "8f4a92c1-9f8b-4f52-9e42-000000000007",
  "channel": {
    "id": "105433958",
    "login": "techmatesracing",
    "displayName": "TechMatesRacing"
  },
  "author": {
    "id": "700000007",
    "login": "long_viewer_name_here",
    "displayName": "long_viewer_name_here",
    "colorHex": "#00E8FF",
    "roles": {
      "broadcaster": false,
      "moderator": false,
      "subscriber": false,
      "vip": true,
      "turbo": false,
      "returningChatter": true
    }
  },
  "badges": [
    { "id": "vip", "version": "1", "label": "vip", "roomId": null },
    { "id": "premium", "version": "1", "label": "premium", "roomId": null }
  ],
  "message": {
    "text": "this is a much longer Twitch chat message that should wrap onto multiple lines instead of clipping the lower half of the text or overflowing horizontally inside the stream chat row cell",
    "segments": [
      {
        "kind": "text",
        "text": "this is a much longer Twitch chat message that should wrap onto multiple lines instead of clipping the lower half of the text or overflowing horizontally inside the stream chat row cell"
      }
    ],
    "emotes": []
  },
  "reply": {
    "parentMessageId": "8f4a92c1-9f8b-4f52-9e42-000000000004",
    "parentUserId": "700000004",
    "parentUserLogin": "crew_chief",
    "parentDisplayName": "crew_chief",
    "parentMessageBody": "Box this lap if traffic stays this bad.",
    "threadParentMessageId": "8f4a92c1-9f8b-4f52-9e42-000000000004",
    "threadParentUserLogin": "crew_chief"
  },
  "rawTags": {
    "badges": "vip/1,premium/1",
    "color": "#00E8FF",
    "display-name": "long_viewer_name_here",
    "id": "8f4a92c1-9f8b-4f52-9e42-000000000007",
    "reply-parent-msg-id": "8f4a92c1-9f8b-4f52-9e42-000000000004",
    "reply-parent-user-id": "700000004",
    "reply-parent-user-login": "crew_chief",
    "reply-parent-display-name": "crew_chief",
    "reply-parent-msg-body": "Box this lap if traffic stays this bad.",
    "reply-thread-parent-msg-id": "8f4a92c1-9f8b-4f52-9e42-000000000004",
    "reply-thread-parent-user-login": "crew_chief",
    "room-id": "105433958",
    "tmi-sent-ts": "1778763240000",
    "user-id": "700000007",
    "vip": "1"
  }
}
```

## Twitch V2 Fixture Rows To Preserve

The future fixture should cover at least these cases:

- Broadcaster/partner/premium normal message.
- Moderator/premium normal message.
- First-time chatter with inline emote.
- Bits cheer with bits badge and emote.
- Resub `USERNOTICE`.
- Raid `USERNOTICE`.
- Long reply message with full parent message fields.

Representative fixture:

```json
{
  "rows": [
    {
      "name": "DuraKitty",
      "kind": "message",
      "source": "twitch",
      "authorColorHex": "#0000FF",
      "metadata": ["11:46"],
      "badges": [
        { "id": "broadcaster", "version": "1", "label": "broadcaster", "roomId": "105433958" },
        { "id": "partner", "version": "1", "label": "partner", "roomId": null },
        { "id": "premium", "version": "1", "label": "premium", "roomId": null }
      ],
      "segments": [{ "kind": "text", "text": "leader is 6.6k", "imageUrl": null }],
      "twitch": {
        "transport": "irc",
        "command": "PRIVMSG",
        "channel": "techmatesracing",
        "tags": {
          "badges": "broadcaster/1,partner/1,premium/1",
          "color": "#0000FF",
          "display-name": "DuraKitty",
          "id": "8f4a92c1-9f8b-4f52-9e42-000000000001",
          "room-id": "105433958",
          "tmi-sent-ts": "1778762700000",
          "user-id": "700000001"
        }
      }
    },
    {
      "name": "new_viewer",
      "kind": "message",
      "source": "twitch",
      "authorColorHex": "#FF7D49",
      "metadata": ["first", "id c0ffee42", "11:49"],
      "segments": [
        { "kind": "text", "text": "First time here and this overlay is clean! ", "imageUrl": null },
        { "kind": "emote", "text": "PogChamp", "imageUrl": "https://static-cdn.jtvnw.net/emoticons/v2/305954156/default/dark/1.0" }
      ],
      "twitch": {
        "transport": "irc",
        "command": "PRIVMSG",
        "channel": "techmatesracing",
        "tags": {
          "emotes": "305954156:43-50",
          "first-msg": "1",
          "id": "8f4a92c1-9f8b-4f52-9e42-000000000003",
          "room-id": "105433958",
          "tmi-sent-ts": "1778762940000"
        },
        "message": {
          "emotes": [{ "id": "305954156", "token": "PogChamp", "start": 43, "end": 50 }]
        }
      }
    },
    {
      "name": "cheer_wall",
      "kind": "message",
      "source": "twitch",
      "authorColorHex": "#FFD15B",
      "metadata": ["100 bits", "11:51"],
      "badges": [{ "id": "bits", "version": "100", "label": "bits", "roomId": null }],
      "segments": [
        { "kind": "text", "text": "100 bits for surviving that stint ", "imageUrl": null },
        { "kind": "emote", "text": "Kappa", "imageUrl": "https://static-cdn.jtvnw.net/emoticons/v2/25/default/dark/1.0" }
      ],
      "twitch": {
        "transport": "irc",
        "command": "PRIVMSG",
        "channel": "techmatesracing",
        "tags": {
          "badge-info": "bits/100",
          "badges": "bits/100",
          "bits": "100",
          "emotes": "25:34-38",
          "id": "8f4a92c1-9f8b-4f52-9e42-000000000004"
        },
        "message": {
          "bits": 100,
          "cheermotes": [{ "prefix": "cheer", "bits": 100 }],
          "emotes": [{ "id": "25", "token": "Kappa", "start": 34, "end": 38 }]
        }
      }
    },
    {
      "name": "sub_event",
      "kind": "notice",
      "source": "twitch",
      "authorColorHex": "#B65CFF",
      "metadata": ["alert resub", "11:52"],
      "badges": [{ "id": "subscriber", "version": "12", "label": "sub 12", "roomId": "105433958" }],
      "segments": [{ "kind": "text", "text": "sub_event subscribed for 12 months Keep pushing.", "imageUrl": null }],
      "twitch": {
        "transport": "irc",
        "command": "USERNOTICE",
        "channel": "techmatesracing",
        "tags": {
          "badge-info": "subscriber/12",
          "badges": "subscriber/12",
          "msg-id": "resub",
          "msg-param-cumulative-months": "12",
          "msg-param-streak-months": "3",
          "msg-param-should-share-streak": "1",
          "msg-param-sub-plan": "1000",
          "msg-param-sub-plan-name": "Tier 1",
          "system-msg": "sub_event\\shas\\ssubscribed\\sfor\\s12\\smonths!"
        },
        "notice": {
          "type": "resub",
          "systemMessage": "sub_event has subscribed for 12 months!",
          "cumulativeMonths": 12,
          "streakMonths": 3,
          "shouldShareStreak": true,
          "subPlan": "1000",
          "subPlanName": "Tier 1",
          "userMessage": "Keep pushing."
        },
        "eventSub": {
          "subscriptionType": "channel.chat.notification",
          "noticeType": "resub",
          "resub": {
            "cumulativeMonths": 12,
            "streakMonths": 3,
            "durationMonths": 1,
            "subTier": "1000",
            "isPrime": false,
            "isGift": false
          }
        }
      }
    },
    {
      "name": "raid_alert",
      "kind": "notice",
      "source": "twitch",
      "authorColorHex": "#9147FF",
      "metadata": ["alert raid", "11:53"],
      "badges": [
        { "id": "vip", "version": "1", "label": "vip", "roomId": null },
        { "id": "turbo", "version": "1", "label": "turbo", "roomId": null }
      ],
      "segments": [{ "kind": "text", "text": "FastPitCrew is raiding with 24 viewers. Welcome in.", "imageUrl": null }],
      "twitch": {
        "transport": "irc",
        "command": "USERNOTICE",
        "channel": "techmatesracing",
        "tags": {
          "badges": "vip/1,turbo/1",
          "msg-id": "raid",
          "msg-param-displayName": "FastPitCrew",
          "msg-param-login": "fastpitcrew",
          "msg-param-profileImageURL": "https://static-cdn.jtvnw.net/jtv_user_pictures/fastpitcrew-profile_image-70x70.png",
          "msg-param-viewerCount": "24",
          "system-msg": "24\\sraiders\\sfrom\\sFastPitCrew\\shave\\sjoined!"
        },
        "notice": {
          "type": "raid",
          "systemMessage": "24 raiders from FastPitCrew have joined!",
          "raiderDisplayName": "FastPitCrew",
          "raiderLogin": "fastpitcrew",
          "viewerCount": 24,
          "profileImageUrl": "https://static-cdn.jtvnw.net/jtv_user_pictures/fastpitcrew-profile_image-70x70.png"
        },
        "eventSub": {
          "subscriptionType": "channel.chat.notification",
          "noticeType": "raid",
          "raid": {
            "userName": "FastPitCrew",
            "userLogin": "fastpitcrew",
            "viewerCount": 24
          }
        }
      }
    },
    {
      "name": "long_viewer_name_here",
      "kind": "message",
      "source": "twitch",
      "authorColorHex": "#00E8FF",
      "metadata": ["reply @crew_chief", "id 9272af30", "11:54"],
      "badges": [
        { "id": "vip", "version": "1", "label": "vip", "roomId": null },
        { "id": "premium", "version": "1", "label": "premium", "roomId": null }
      ],
      "segments": [{ "kind": "text", "text": "this is a much longer Twitch chat message that should wrap onto multiple lines instead of clipping the lower half of the text or overflowing horizontally inside the stream chat row cell", "imageUrl": null }],
      "twitch": {
        "transport": "irc",
        "command": "PRIVMSG",
        "channel": "techmatesracing",
        "tags": {
          "reply-parent-msg-id": "8f4a92c1-9f8b-4f52-9e42-000000000004",
          "reply-parent-user-id": "700000004",
          "reply-parent-user-login": "crew_chief",
          "reply-parent-display-name": "crew_chief",
          "reply-parent-msg-body": "Box this lap if traffic stays this bad.",
          "reply-thread-parent-msg-id": "8f4a92c1-9f8b-4f52-9e42-000000000004",
          "reply-thread-parent-user-login": "crew_chief"
        },
        "reply": {
          "parentMessageId": "8f4a92c1-9f8b-4f52-9e42-000000000004",
          "parentUserId": "700000004",
          "parentUserLogin": "crew_chief",
          "parentDisplayName": "crew_chief",
          "parentMessageBody": "Box this lap if traffic stays this bad.",
          "threadParentMessageId": "8f4a92c1-9f8b-4f52-9e42-000000000004",
          "threadParentUserLogin": "crew_chief"
        }
      }
    }
  ]
}
```

## Streamlabs Data Surfaces

Streamlabs should not be treated as a Twitch IRC equivalent.

Mode 1: Streamlabs Chat Box URL

- This is an opaque hosted browser widget.
- TMR can validate/store/embed the widget URL.
- Streamlabs controls chat platforms, badges, extra emotes, filters, hide delay, and widget styling.
- TMR cannot reliably inspect or restyle internal rows from the parent localhost page because the widget is cross-origin.

Mode 2: Streamlabs Socket API

- This is an authenticated event feed.
- It requires a Streamlabs app/OAuth flow, `socket.token`, token storage, and Socket.IO connection handling.
- The documented payloads are event/alert oriented, not a general structured chat-message feed.
- `eventData.message` is documented as an array.
- Useful events include donation, Twitch follow, Twitch subscription, Twitch host, Twitch bits, Twitch raids, YouTube subscription, YouTube sponsor, and YouTube superchat.

Streamlabs V2 should likely use a separate `StreamEvent` model rather than forcing alerts into `StreamChatMessage`.

## Proposed Streamlabs V2 Event Model

```json
{
  "provider": "streamlabs",
  "transport": "socket-api",
  "eventType": "donation",
  "sourcePlatform": "streamlabs",
  "id": "streamlabs-event-1",
  "actor": {
    "displayName": "PitWallFan",
    "userId": null
  },
  "message": {
    "text": "Fuel money for the next stop."
  },
  "amount": {
    "value": 10,
    "formatted": "$10.00",
    "currency": "USD"
  },
  "occurredAtUtc": null,
  "raw": {
    "type": "donation",
    "for": "streamlabs",
    "message": [
      {
        "id": 1001,
        "name": "PitWallFan",
        "amount": "10.00",
        "formatted_amount": "$10.00",
        "message": "Fuel money for the next stop.",
        "currency": "USD",
        "from": "PitWallFan",
        "from_user_id": null,
        "_id": "donation-1001",
        "event_id": "streamlabs-event-1"
      }
    ]
  }
}
```

Recommended Streamlabs V2 toggles:

- Donations/tips.
- Follows.
- Subs/resubs/gifts.
- Bits.
- Raids/hosts.
- YouTube superchats.
- Amounts.
- Viewer messages.
- Source platform.
- Test/debug event IDs.

Avoid applying Twitch-specific toggles to Streamlabs until verified by real payloads:

- Twitch author color.
- Twitch badges.
- Twitch first-message.
- Twitch replies.
- Twitch IRC emote ranges.
- Twitch message IDs.

## Streamlabs V2 Mock Events

```json
{
  "events": [
    {
      "type": "donation",
      "for": "streamlabs",
      "message": [
        {
          "id": 1001,
          "name": "PitWallFan",
          "amount": "10.00",
          "formatted_amount": "$10.00",
          "message": "Fuel money for the next stop.",
          "currency": "USD",
          "from": "PitWallFan",
          "from_user_id": null,
          "_id": "donation-1001",
          "event_id": "streamlabs-event-1"
        }
      ]
    },
    {
      "type": "subscription",
      "for": "twitch_account",
      "message": [
        {
          "name": "sub_event",
          "months": 12,
          "message": "Keep pushing.",
          "sub_plan": "1000",
          "sub_plan_name": "Tier 1",
          "is_gift": false,
          "event_id": "streamlabs-event-2"
        }
      ]
    },
    {
      "type": "bits",
      "for": "twitch_account",
      "message": [
        {
          "name": "cheer_wall",
          "amount": 100,
          "message": "100 bits for surviving that stint",
          "event_id": "streamlabs-event-3"
        }
      ]
    },
    {
      "type": "raids",
      "for": "twitch_account",
      "message": [
        {
          "name": "FastPitCrew",
          "viewers": 24,
          "message": "Welcome in.",
          "event_id": "streamlabs-event-4"
        }
      ]
    },
    {
      "type": "superchat",
      "for": "youtube_account",
      "message": [
        {
          "name": "YTViewer",
          "amount": "5.00",
          "formatted_amount": "$5.00",
          "currency": "USD",
          "message": "Great stint.",
          "event_id": "streamlabs-event-5"
        }
      ]
    }
  ]
}
```

## Open V2 Questions

- Do we want Twitch IRC only, EventSub only, or both?
- Should `USERNOTICE` rows be styled as chat rows, alert cards, or compact system rows?
- Should reply previews show the parent message body, or only the parent user?
- How much badge/emote fetching should native do, and where should cache live?
- Should Streamlabs widget mode remain a fully opaque embed, or should we build an authenticated Streamlabs events overlay as a separate product?
- How should stream events interact with fixed overlay height and row pruning?

## Sources

- Twitch IRC concepts and tags: https://dev.twitch.tv/docs/irc/authenticate-bot
- Twitch EventSub subscription types: https://dev.twitch.tv/docs/eventsub/eventsub-subscription-types/
- Twitch EventSub reference: https://dev.twitch.tv/docs/eventsub/eventsub-reference/
- Streamlabs Socket API: https://dev.streamlabs.com/docs/socket-api
- Streamlabs widget URL support: https://support.streamlabs.com/hc/en-us/articles/41706358816667-How-to-Locate-and-Use-Streamlabs-Widget-URLs
- Streamlabs Chat Box widget setup: https://streamlabs.com/content-hub/post/chatbox-widget-setup
