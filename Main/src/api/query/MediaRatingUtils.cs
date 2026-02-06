using io.wispforest.endec;
using io.wispforest.endec.impl;

namespace io.wispforest.textureswapper.api.query;

public class MediaRatingUtils {
    public static readonly Endec<MediaRating> ENDEC = endec.Endec.STRING.xmap(toMediaRating, toCharacter);

    public static string toCharacter(MediaRating rating) {
        return rating switch {
            MediaRating.SAFE => "s",
            MediaRating.QUESTIONABLE => "q",
            MediaRating.EXPLICIT => "e",
            MediaRating.UNKNOWN => "u"
        };
    }
    
    public static MediaRating toMediaRating(string rating) {
        return rating.ToLower() switch {
                "s" or "safe" =>  MediaRating.SAFE,
                "q" or "questionable" => MediaRating.QUESTIONABLE,
                "u" or "unknown" => MediaRating.UNKNOWN,
                _ => MediaRating.EXPLICIT,
        };
    }
}

public enum MediaRating {
    SAFE,
    QUESTIONABLE,
    EXPLICIT,
    UNKNOWN
}