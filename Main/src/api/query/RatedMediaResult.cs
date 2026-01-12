namespace io.wispforest.textureswapper.api.query;

public interface RatedMediaResult {
    MediaRating getRating();

    public bool isSafe() {
        return getRating().Equals(MediaRating.SAFE);
    }
}