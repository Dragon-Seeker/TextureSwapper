namespace io.wispforest.textureswapper.api.query;

public interface RatedMediaResult {
    MediaRating rating { get; }

    public bool isSafe() {
        return rating.Equals(MediaRating.SAFE);
    }
}