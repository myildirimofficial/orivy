using System.Collections.Generic;

namespace Orivy.Controls.Markdown;

/// <summary>Translates GitHub-style emoji shortcodes (:smile:) to their Unicode equivalents.</summary>
internal static class MarkdownEmojiTable
{
    private static readonly Dictionary<string, string> Table = new(System.StringComparer.Ordinal)
    {
        // Faces
        ["grinning"] = "😀", ["smile"] = "😄", ["smiley"] = "😃", ["grin"] = "😁",
        ["laughing"] = "😆", ["sweat_smile"] = "😅", ["rofl"] = "🤣", ["joy"] = "😂",
        ["slightly_smiling_face"] = "🙂", ["upside_down_face"] = "🙃", ["wink"] = "😉",
        ["blush"] = "😊", ["innocent"] = "😇", ["smiling_face_with_three_hearts"] = "🥰",
        ["heart_eyes"] = "😍", ["kissing_heart"] = "😘", ["kissing"] = "😗",
        ["kissing_smiling_eyes"] = "😙", ["kissing_closed_eyes"] = "😚",
        ["yum"] = "😋", ["stuck_out_tongue"] = "😛", ["stuck_out_tongue_winking_eye"] = "😜",
        ["zany_face"] = "🤪", ["stuck_out_tongue_closed_eyes"] = "😝", ["money_mouth_face"] = "🤑",
        ["hugs"] = "🤗", ["thinking"] = "🤔", ["zipper_mouth_face"] = "🤐",
        ["raised_eyebrow"] = "🤨", ["neutral_face"] = "😐", ["expressionless"] = "😑",
        ["no_mouth"] = "😶", ["smirk"] = "😏", ["unamused"] = "😒",
        ["roll_eyes"] = "🙄", ["grimacing"] = "😬", ["lying_face"] = "🤥",
        ["relieved"] = "😌", ["pensive"] = "😔", ["sleepy"] = "😪",
        ["drooling_face"] = "🤤", ["sleeping"] = "😴", ["mask"] = "😷",
        ["face_with_thermometer"] = "🤒", ["face_with_head_bandage"] = "🤕",
        ["nauseated_face"] = "🤢", ["sneezing_face"] = "🤧", ["hot_face"] = "🥵",
        ["cold_face"] = "🥶", ["woozy_face"] = "🥴", ["dizzy_face"] = "😵",
        ["exploding_head"] = "🤯", ["cowboy_hat_face"] = "🤠", ["partying_face"] = "🥳",
        ["sunglasses"] = "😎", ["nerd_face"] = "🤓", ["monocle_face"] = "🧐",
        ["confused"] = "😕", ["worried"] = "😟", ["slightly_frowning_face"] = "🙁",
        ["frowning_face"] = "☹", ["open_mouth"] = "😮", ["hushed"] = "😯",
        ["astonished"] = "😲", ["flushed"] = "😳", ["pleading_face"] = "🥺",
        ["anguished"] = "😧", ["fearful"] = "😨", ["cold_sweat"] = "😰",
        ["disappointed_relieved"] = "😥", ["cry"] = "😢", ["sob"] = "😭",
        ["scream"] = "😱", ["confounded"] = "😖", ["persevere"] = "😣",
        ["disappointed"] = "😞", ["sweat"] = "😓", ["weary"] = "😩",
        ["tired_face"] = "😫", ["yawning_face"] = "🥱", ["triumph"] = "😤",
        ["rage"] = "😡", ["angry"] = "😠", ["skull"] = "💀", ["skull_and_crossbones"] = "☠",
        ["poop"] = "💩", ["clown_face"] = "🤡", ["japanese_ogre"] = "👹",
        ["japanese_goblin"] = "👺", ["ghost"] = "👻", ["alien"] = "👽",
        ["space_invader"] = "👾", ["robot"] = "🤖",

        // Hands & gestures
        ["+1"] = "👍", ["thumbsup"] = "👍", ["-1"] = "👎", ["thumbsdown"] = "👎",
        ["clap"] = "👏", ["raised_hands"] = "🙌", ["pray"] = "🙏",
        ["handshake"] = "🤝", ["point_up"] = "☝", ["point_up_2"] = "👆",
        ["point_down"] = "👇", ["point_left"] = "👈", ["point_right"] = "👉",
        ["fu"] = "🖕", ["raised_hand"] = "✋", ["v"] = "✌", ["ok_hand"] = "👌",
        ["wave"] = "👋", ["metal"] = "🤘", ["crossed_fingers"] = "🤞",
        ["vulcan_salute"] = "🖖", ["writing_hand"] = "✍", ["muscle"] = "💪",
        ["selfie"] = "🤳", ["ring"] = "💍", ["nail_care"] = "💅",

        // Hearts & emotions
        ["heart"] = "❤", ["orange_heart"] = "🧡", ["yellow_heart"] = "💛",
        ["green_heart"] = "💚", ["blue_heart"] = "💙", ["purple_heart"] = "💜",
        ["black_heart"] = "🖤", ["broken_heart"] = "💔", ["heavy_heart_exclamation"] = "❣",
        ["two_hearts"] = "💕", ["revolving_hearts"] = "💞", ["heartbeat"] = "💓",
        ["heartpulse"] = "💗", ["sparkling_heart"] = "💖", ["cupid"] = "💘",
        ["gift_heart"] = "💝", ["heart_decoration"] = "💟", ["peace_symbol"] = "☮",
        ["100"] = "💯", ["fire"] = "🔥", ["star"] = "⭐", ["star2"] = "🌟",
        ["dizzy"] = "💫", ["sparkles"] = "✨", ["boom"] = "💥", ["tada"] = "🎉",
        ["confetti_ball"] = "🎊", ["balloon"] = "🎈",

        // Nature
        ["sunny"] = "☀", ["cloud"] = "☁", ["umbrella"] = "☂", ["snowflake"] = "❄",
        ["snowman"] = "⛄", ["zap"] = "⚡", ["cyclone"] = "🌀", ["rainbow"] = "🌈",
        ["ocean"] = "🌊", ["droplet"] = "💧", ["sweat_drops"] = "💦",
        ["dog"] = "🐶", ["cat"] = "🐱", ["mouse"] = "🐭", ["hamster"] = "🐹",
        ["rabbit"] = "🐰", ["fox_face"] = "🦊", ["bear"] = "🐻", ["panda_face"] = "🐼",
        ["koala"] = "🐨", ["tiger"] = "🐯", ["lion"] = "🦁", ["cow"] = "🐮",
        ["pig"] = "🐷", ["frog"] = "🐸", ["monkey_face"] = "🐵", ["chicken"] = "🐔",
        ["penguin"] = "🐧", ["bird"] = "🐦", ["baby_chick"] = "🐤", ["hatching_chick"] = "🐣",
        ["duck"] = "🦆", ["eagle"] = "🦅", ["owl"] = "🦉", ["bat"] = "🦇",
        ["wolf"] = "🐺", ["boar"] = "🐗", ["horse"] = "🐴", ["unicorn"] = "🦄",
        ["bee"] = "🐝", ["bug"] = "🐛", ["butterfly"] = "🦋", ["snail"] = "🐌",
        ["shell"] = "🐚", ["crab"] = "🦀", ["shrimp"] = "🦐", ["squid"] = "🦑",
        ["octopus"] = "🐙", ["turtle"] = "🐢", ["snake"] = "🐍", ["dragon"] = "🐲",
        ["whale"] = "🐳", ["dolphin"] = "🐬", ["fish"] = "🐟", ["blowfish"] = "🐡",
        ["tropical_fish"] = "🐠", ["shark"] = "🦈", ["crocodile"] = "🐊",
        ["rose"] = "🌹", ["sunflower"] = "🌻", ["four_leaf_clover"] = "🍀",
        ["maple_leaf"] = "🍁", ["fallen_leaf"] = "🍂", ["leaves"] = "🍃",
        ["mushroom"] = "🍄", ["cactus"] = "🌵", ["palm_tree"] = "🌴",
        ["seedling"] = "🌱", ["herb"] = "🌿", ["shamrock"] = "☘",

        // Food
        ["apple"] = "🍎", ["green_apple"] = "🍏", ["pear"] = "🍐", ["tangerine"] = "🍊",
        ["lemon"] = "🍋", ["banana"] = "🍌", ["watermelon"] = "🍉", ["grapes"] = "🍇",
        ["strawberry"] = "🍓", ["melon"] = "🍈", ["cherries"] = "🍒", ["peach"] = "🍑",
        ["pineapple"] = "🍍", ["pizza"] = "🍕", ["hamburger"] = "🍔", ["hotdog"] = "🌭",
        ["taco"] = "🌮", ["burrito"] = "🌯", ["sandwich"] = "🥪", ["cookie"] = "🍪",
        ["cake"] = "🎂", ["chocolate_bar"] = "🍫", ["candy"] = "🍬", ["lollipop"] = "🍭",
        ["coffee"] = "☕", ["tea"] = "🍵", ["beer"] = "🍺", ["beers"] = "🍻",
        ["wine_glass"] = "🍷", ["cocktail"] = "🍸", ["tropical_drink"] = "🍹",
        ["icecream"] = "🍦", ["ice_cream"] = "🍨", ["doughnut"] = "🍩",

        // Objects & symbols
        ["bulb"] = "💡", ["flashlight"] = "🔦", ["candle"] = "🕯",
        ["computer"] = "💻", ["desktop_computer"] = "🖥", ["keyboard"] = "⌨",
        ["phone"] = "📱", ["telephone"] = "☎", ["mail"] = "📧", ["email"] = "📧",
        ["memo"] = "📝", ["pencil"] = "✏", ["pen"] = "🖊", ["book"] = "📖",
        ["books"] = "📚", ["notebook"] = "📓", ["clipboard"] = "📋",
        ["calendar"] = "📅", ["date"] = "📅", ["clock"] = "🕐", ["alarm_clock"] = "⏰",
        ["stopwatch"] = "⏱", ["timer_clock"] = "⏲", ["hourglass"] = "⌛",
        ["camera"] = "📷", ["video_camera"] = "📹", ["tv"] = "📺",
        ["radio"] = "📻", ["headphones"] = "🎧", ["microphone"] = "🎤",
        ["guitar"] = "🎸", ["musical_note"] = "🎵", ["notes"] = "🎶",
        ["rocket"] = "🚀", ["airplane"] = "✈", ["car"] = "🚗", ["taxi"] = "🚕",
        ["bus"] = "🚌", ["train"] = "🚂", ["bicycle"] = "🚲", ["anchor"] = "⚓",
        ["construction"] = "🚧", ["warning"] = "⚠", ["no_entry"] = "⛔",
        ["lock"] = "🔒", ["unlock"] = "🔓", ["key"] = "🔑", ["hammer"] = "🔨",
        ["wrench"] = "🔧", ["scissors"] = "✂", ["paperclip"] = "📎",
        ["pushpin"] = "📌", ["round_pushpin"] = "📍", ["link"] = "🔗",
        ["mag"] = "🔍", ["money_bag"] = "💰", ["credit_card"] = "💳",
        ["gem"] = "💎", ["trophy"] = "🏆", ["medal_sports"] = "🏅",
        ["dart"] = "🎯", ["video_game"] = "🎮", ["joystick"] = "🕹",
        ["spades"] = "♠", ["hearts"] = "♥", ["diamonds"] = "♦", ["clubs"] = "♣",
        ["chess_pawn"] = "♟", ["jigsaw"] = "🧩",
        ["red_circle"] = "🔴", ["orange_circle"] = "🟠", ["yellow_circle"] = "🟡",
        ["green_circle"] = "🟢", ["blue_circle"] = "🔵", ["purple_circle"] = "🟣",
        ["white_circle"] = "⚪", ["black_circle"] = "⚫",
        ["check"] = "✔", ["heavy_check_mark"] = "✅", ["x"] = "❌",
        ["heavy_multiplication_x"] = "✖", ["question"] = "❓", ["exclamation"] = "❗",
        ["bangbang"] = "‼", ["grey_question"] = "❔", ["grey_exclamation"] = "❕",
        ["information_source"] = "ℹ", ["recycle"] = "♻", ["sos"] = "🆘",
        ["new"] = "🆕", ["up"] = "🆙", ["cool"] = "🆒", ["free"] = "🆓",
        ["ng"] = "🆖", ["ok"] = "🆗",
        ["arrow_up"] = "⬆", ["arrow_down"] = "⬇", ["arrow_left"] = "⬅",
        ["arrow_right"] = "➡", ["arrow_forward"] = "▶", ["arrow_backward"] = "◀",
        ["rewind"] = "⏪", ["fast_forward"] = "⏩", ["camel"] = "🐫",
    };

    /// <summary>Returns the emoji string for a shortcode (without colons), or null if not found.</summary>
    public static string? Lookup(string code) =>
        Table.TryGetValue(code, out var emoji) ? emoji : null;

    /// <summary>Returns true if the Unicode code point is likely to need an emoji font to render.</summary>
    public static bool IsEmojiCodePoint(int codePoint)
    {
        return codePoint is
            (>= 0x1F000 and <= 0x1FFFF) or // Emoji & supplemental symbols
            (>= 0x2600 and <= 0x27FF) or   // Misc symbols & dingbats
            (>= 0xFE0F and <= 0xFE0F) or   // Variation selector-16 (emoji style)
            0x200D;                          // Zero-width joiner (emoji sequences)
    }
}
