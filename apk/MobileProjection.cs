namespace HyperLane.Android;

// Matches the Windows renderer's 26-degree vertical frustum and camera basis.
internal sealed class MobileProjection
{
    private readonly Vec3 camera;
    private readonly Vec3 forward;
    private readonly Vec3 right;
    private readonly Vec3 up;
    private readonly double focalY = 540 / Math.Tan(Math.PI * 13 / 180);

    private MobileProjection(Vec3 camera, Vec3 forward, Vec3 right, Vec3 up)
    { this.camera = camera; this.forward = forward; this.right = right; this.up = up; }

    public static MobileProjection At(MobileDifficulty difficulty, double beat)
    {
        CameraPose from = new(0, 2.5, -6, 0, 0, 0, false);
        CameraPose to = from;
        double start = 0, duration = 0;
        int mode = 1;
        bool advanced = false, returningToNormal = false;
        foreach (MobileCameraChange change in difficulty.CameraChanges)
        {
            if (change.Beat > beat && !(beat<0&&change.Beat==0&&change.Duration==0)) {if(beat<0&&change.Beat==0)continue;break;}
            from = Interpolate(from, to, start, duration, mode, change.Beat);
            if (change.Advanced && !advanced) from = LookPose(from);
            returningToNormal = advanced && !change.Advanced;
            to = change.Advanced ? new CameraPose(change.X, change.Y, change.Z, change.Rx, change.Ry, change.Rz, true)
                : LookPose(new CameraPose(change.X, change.Y, change.Z, 0, 0, 0, false));
            advanced = change.Advanced;
            start = change.Beat;
            duration = change.Duration;
            mode = change.Mode;
        }
        CameraPose pose = Interpolate(from, to, start, duration, mode, beat);
        pose = pose with { Advanced = advanced || (returningToNormal && duration > 0 && beat < start + duration) };
        Vec3 location = new(pose.X, pose.Y, pose.Z);
        Vec3 forward;
        if (pose.Advanced)
        {
            double rx = pose.Rx * Math.PI / 180, ry = pose.Ry * Math.PI / 180;
            forward = Normalize(new(Math.Cos(rx) * Math.Sin(ry), Math.Sin(rx), Math.Cos(rx) * Math.Cos(ry)));
        }
        else forward = Normalize(new(-pose.X, -pose.Y, 5 - pose.Z));
        Vec3 right = Normalize(new(-forward.Z, 0, forward.X));
        Vec3 up = Cross(right, forward);
        if (pose.Advanced && Math.Abs(pose.Rz) > .0001)
        {
            double angle = pose.Rz * Math.PI / 180;
            Vec3 priorRight = right;
            right = right * Math.Cos(angle) + up * Math.Sin(angle);
            up = up * Math.Cos(angle) - priorRight * Math.Sin(angle);
        }
        return new MobileProjection(location, forward, right, up);
    }

    public bool Project(double x, double y, double z, out float screenX, out float screenY)
    {
        Vec3 relative = new Vec3(x, y, z) - camera;
        double depth = Dot(relative, forward);
        if (depth <= .1 || !double.IsFinite(depth))
        { screenX = screenY = 0; return false; }
        screenX = (float)(960 + Dot(relative, right) * focalY / depth);
        screenY = (float)(540 - Dot(relative, up) * focalY / depth);
        return float.IsFinite(screenX) && float.IsFinite(screenY);
    }

    internal int HitLane(int lanes, float screenX, float screenY)
    {
        if (!TouchZones.Supports(lanes) || screenX < 0 || screenX >= 1920 || screenY < 0 || screenY >= 1080) return -1;
        Vec3 ray = forward + right * ((screenX - 960) / focalY) + up * ((540 - screenY) / focalY);
        if (Math.Abs(ray.Y) < .000001) return -1;
        double t = -camera.Y / ray.Y;
        if (t <= 0) return -1;
        Vec3 hit = camera + ray * t;
        if (hit.X < -1.15 || hit.X >= 1.15 || hit.Z < -12 || hit.Z > 52) return -1;
        return Math.Clamp((int)((1.15 - hit.X) / (2.3 / lanes)), 0, lanes - 1);
    }

    public void Clip(double x, double y, double z, Span<float> clip)
    {
        Vec3 relative = new Vec3(x, y, z) - camera;
        double depth = Dot(relative, forward);
        clip[0] = (float)(Dot(relative, right) / Math.Tan(Math.PI * 13 / 180) / (1920.0 / 1080));
        clip[1] = (float)(Dot(relative, up) / Math.Tan(Math.PI * 13 / 180));
        clip[2] = (float)((200.0 + .1) / (200.0 - .1) * depth - 2 * 200.0 * .1 / (200.0 - .1));
        clip[3] = (float)depth;
    }

    public bool Quad(double x0, double x1, double y, double nearZ, double farZ, out float[] points)
    {
        points = new float[8];
        if (!Project(x0, y, farZ, out points[0], out points[1]) ||
            !Project(x1, y, farZ, out points[2], out points[3]) ||
            !Project(x1, y, nearZ, out points[4], out points[5]) ||
            !Project(x0, y, nearZ, out points[6], out points[7])) return false;
        return true;
    }

    public double DepthAtScreenFraction(double fraction)
    {
        if (fraction <= 0) return 0;
        if (fraction >= 1) return 45;
        if (!Project(0, 0, 0, out _, out float nearY) || !Project(0, 0, 45, out _, out float farY) || Math.Abs(farY - nearY) < 1)
            return 45 * fraction;
        double target = nearY + (farY - nearY) * fraction, low = 0, high = 45;
        for (int i = 0; i < 18; i++)
        {
            double middle = (low + high) * .5;
            if (!Project(0, 0, middle, out _, out float y)) break;
            if ((y - target) * (farY - nearY) < 0) low = middle; else high = middle;
        }
        return (low + high) * .5;
    }

    // Clip world polygons before projection: a track extends behind the camera.
    public float[] Polygon(params double[] coordinates)
    {
        var vertices = new List<Vec3>();
        for (int i = 0; i + 2 < coordinates.Length; i += 3) vertices.Add(new(coordinates[i], coordinates[i + 1], coordinates[i + 2]));
        var clipped = new List<Vec3>();
        for (int i = 0; i < vertices.Count; i++)
        {
            Vec3 a = vertices[i], b = vertices[(i + 1) % vertices.Count];
            double da = Dot(a - camera, forward), db = Dot(b - camera, forward);
            if (da >= .10001) clipped.Add(a);
            if ((da >= .10001) != (db >= .10001)) clipped.Add(a + (b - a) * ((.10001 - da) / (db - da)));
        }
        var result = new List<float>();
        foreach (Vec3 vertex in clipped)
            if (Project(vertex.X, vertex.Y, vertex.Z, out float x, out float y)) { result.Add(x); result.Add(y); }
        return result.ToArray();
    }

    private static CameraPose LookPose(CameraPose pose)
    {
        double dx = -pose.X, dy = -pose.Y, dz = 5 - pose.Z;
        double length = Math.Max(1e-9, Math.Sqrt(dx * dx + dy * dy + dz * dz));
        return pose with { Rx = Math.Asin(dy / length) * 180 / Math.PI, Ry = Math.Atan2(dx, dz) * 180 / Math.PI, Rz = 0 };
    }

    private static CameraPose Interpolate(CameraPose from, CameraPose to, double start, double duration, int mode, double beat)
    {
        double t = duration <= 0 ? 1 : Math.Clamp((beat - start) / duration, 0, 1);
        t = mode switch { 2 => t * t * t, 3 => 1 - Math.Pow(1 - t, 3), _ => t };
        return new CameraPose(
            from.X + (to.X - from.X) * t, from.Y + (to.Y - from.Y) * t,
            from.Z + (to.Z - from.Z) * t, from.Rx + (to.Rx - from.Rx) * t,
            from.Ry + (to.Ry - from.Ry) * t, from.Rz + (to.Rz - from.Rz) * t,
            t < 1 ? from.Advanced || to.Advanced : to.Advanced);
    }

    private static double Dot(Vec3 a, Vec3 b) => a.X * b.X + a.Y * b.Y + a.Z * b.Z;
    private static Vec3 Cross(Vec3 a, Vec3 b) => new(a.Y * b.Z - a.Z * b.Y,
        a.Z * b.X - a.X * b.Z, a.X * b.Y - a.Y * b.X);
    private static Vec3 Normalize(Vec3 v)
    {
        double length = Math.Sqrt(Dot(v, v));
        // At +/-90 degrees pitch the horizontal basis is tiny but still valid:
        // cos(90 degrees) is about 6e-17. Replacing it with +Z rotates the
        // camera sideways. Normalize it just as the PC renderer does.
        return length <= 0 || !double.IsFinite(length) ? new Vec3(0, 0, 1) : v * (1 / length);
    }

    private readonly record struct CameraPose(double X, double Y, double Z,
        double Rx, double Ry, double Rz, bool Advanced);
    private readonly record struct Vec3(double X, double Y, double Z)
    {
        public static Vec3 operator +(Vec3 a, Vec3 b) => new(a.X + b.X, a.Y + b.Y, a.Z + b.Z);
        public static Vec3 operator -(Vec3 a, Vec3 b) => new(a.X - b.X, a.Y - b.Y, a.Z - b.Z);
        public static Vec3 operator *(Vec3 a, double scale) => new(a.X * scale, a.Y * scale, a.Z * scale);
    }
}
