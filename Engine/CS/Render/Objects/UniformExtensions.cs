using OpenTK.Graphics.OpenGL4;

namespace Patchwork.Render.Objects
{
    public static class UniformExtensions
    {
        public static bool SkipMissingUniforms { get; set; } = true;

        private static readonly Dictionary<int, Dictionary<string, int>> Cache = new();

        private static int GetLocation(this Shader s, string name)
        {
            if (s == null) throw new ArgumentNullException(nameof(s));
            if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException(nameof(name));
            if (s.Id == 0) throw new InvalidOperationException("Program not linked or disposed.");
            if (!Cache.TryGetValue(s.Id, out Dictionary<string, int>? dict))
            {
                dict = new Dictionary<string, int>(StringComparer.Ordinal);
                Cache[s.Id] = dict;
            }
            if (dict.TryGetValue(name, out int loc)) return loc;
            loc = GL.GetUniformLocation(s.Id, name);
            dict[name] = loc;
            return loc;
        }

        private static bool TryLoc(Shader s, string name, out int loc)
        {
            loc = s.GetLocation(name);
            if (loc < 0 && !SkipMissingUniforms)
                throw new InvalidOperationException($"Uniform '{name}' not found in program {s.Id}.");
            return loc >= 0;
        }

        public static Shader Set(this Shader s, string name, float v) { if (TryLoc(s, name, out int loc)) GL.Uniform1(loc, v); return s; }
        public static Shader Set(this Shader s, string name, double v) { if (TryLoc(s, name, out int loc)) GL.Uniform1(loc, v); return s; }
        public static Shader Set(this Shader s, string name, int v) { if (TryLoc(s, name, out int loc)) GL.Uniform1(loc, v); return s; }
        public static Shader Set(this Shader s, string name, uint v) { if (TryLoc(s, name, out int loc)) GL.Uniform1(loc, unchecked((int)v)); return s; }
        public static Shader Set(this Shader s, string name, bool v) { if (TryLoc(s, name, out int loc)) GL.Uniform1(loc, v ? 1 : 0); return s; }

        public static Shader Set(this Shader s, string name, IEnumerable<float> values)
        { if (!TryLoc(s, name, out int loc)) return s; float[] arr = values as float[] ?? values.ToArray(); if (arr.Length > 0) GL.Uniform1(loc, arr.Length, arr); return s; }
        public static Shader Set(this Shader s, string name, IEnumerable<double> values)
        { if (!TryLoc(s, name, out int loc)) return s; double[] arr = values as double[] ?? values.ToArray(); if (arr.Length > 0) GL.Uniform1(loc, arr.Length, arr); return s; }
        public static Shader Set(this Shader s, string name, IEnumerable<int> values)
        { if (!TryLoc(s, name, out int loc)) return s; int[] arr = values as int[] ?? values.ToArray(); if (arr.Length > 0) GL.Uniform1(loc, arr.Length, arr); return s; }
        public static Shader Set(this Shader s, string name, IEnumerable<uint> values)
        { if (!TryLoc(s, name, out int loc)) return s; int[] arr = values.Select(x => unchecked((int)x)).ToArray(); if (arr.Length > 0) GL.Uniform1(loc, arr.Length, arr); return s; }
        public static Shader Set(this Shader s, string name, IEnumerable<bool> values)
        { if (!TryLoc(s, name, out int loc)) return s; int[] arr = values.Select(b => b ? 1 : 0).ToArray(); if (arr.Length > 0) GL.Uniform1(loc, arr.Length, arr); return s; }

        public static Shader Set(this Shader s, string name, Vector2 v)
        { if (TryLoc(s, name, out int loc)) GL.Uniform2(loc, v.X, v.Y); return s; }
        public static Shader Set(this Shader s, string name, Vector2i v)
        { if (TryLoc(s, name, out int loc)) GL.Uniform2(loc, v.X, v.Y); return s; }
        public static Shader Set(this Shader s, string name, Vector2d v)
        { if (TryLoc(s, name, out int loc)) GL.Uniform2(loc, v.X, v.Y); return s; }

        public static Shader Set(this Shader s, string name, IEnumerable<Vector2> values)
        { if (!TryLoc(s, name, out int loc)) return s; float[] flat = Flatten(values); if (flat.Length > 0) GL.Uniform2(loc, flat.Length / 2, flat); return s; }
        public static Shader Set(this Shader s, string name, IEnumerable<Vector2i> values)
        { if (!TryLoc(s, name, out int loc)) return s; int[] flat = Flatten(values); if (flat.Length > 0) GL.Uniform2(loc, flat.Length / 2, flat); return s; }
        public static Shader Set(this Shader s, string name, IEnumerable<Vector2d> values)
        { if (!TryLoc(s, name, out int loc)) return s; double[] flat = Flatten(values); if (flat.Length > 0) GL.Uniform2(loc, flat.Length / 2, flat); return s; }

        public static Shader Set(this Shader s, string name, Vector3 v)
        { if (TryLoc(s, name, out int loc)) GL.Uniform3(loc, v.X, v.Y, v.Z); return s; }
        public static Shader Set(this Shader s, string name, Vector3i v)
        { if (TryLoc(s, name, out int loc)) GL.Uniform3(loc, v.X, v.Y, v.Z); return s; }
        public static Shader Set(this Shader s, string name, Vector3d v)
        { if (TryLoc(s, name, out int loc)) GL.Uniform3(loc, v.X, v.Y, v.Z); return s; }

        public static Shader Set(this Shader s, string name, IEnumerable<Vector3> values)
        { if (!TryLoc(s, name, out int loc)) return s; float[] flat = Flatten(values); if (flat.Length > 0) GL.Uniform3(loc, flat.Length / 3, flat); return s; }
        public static Shader Set(this Shader s, string name, IEnumerable<Vector3i> values)
        { if (!TryLoc(s, name, out int loc)) return s; int[] flat = Flatten(values); if (flat.Length > 0) GL.Uniform3(loc, flat.Length / 3, flat); return s; }
        public static Shader Set(this Shader s, string name, IEnumerable<Vector3d> values)
        { if (!TryLoc(s, name, out int loc)) return s; double[] flat = Flatten(values); if (flat.Length > 0) GL.Uniform3(loc, flat.Length / 3, flat); return s; }

        public static Shader Set(this Shader s, string name, Vector4 v)
        { if (TryLoc(s, name, out int loc)) GL.Uniform4(loc, v.X, v.Y, v.Z, v.W); return s; }
        public static Shader Set(this Shader s, string name, Vector4i v)
        { if (TryLoc(s, name, out int loc)) GL.Uniform4(loc, v.X, v.Y, v.Z, v.W); return s; }
        public static Shader Set(this Shader s, string name, Vector4d v)
        { if (TryLoc(s, name, out int loc)) GL.Uniform4(loc, v.X, v.Y, v.Z, v.W); return s; }
        public static Shader Set(this Shader s, string name, Box v)
        { if (TryLoc(s, name, out int loc)) GL.Uniform4(loc, v.X, v.Y, v.Width, v.Height); return s; }

        public static Shader Set(this Shader s, string name, IEnumerable<Vector4> values)
        { if (!TryLoc(s, name, out int loc)) return s; float[] flat = Flatten(values); if (flat.Length > 0) GL.Uniform4(loc, flat.Length / 4, flat); return s; }
        public static Shader Set(this Shader s, string name, IEnumerable<Vector4i> values)
        { if (!TryLoc(s, name, out int loc)) return s; int[] flat = Flatten(values); if (flat.Length > 0) GL.Uniform4(loc, flat.Length / 4, flat); return s; }
        public static Shader Set(this Shader s, string name, IEnumerable<Vector4d> values)
        { if (!TryLoc(s, name, out int loc)) return s; double[] flat = Flatten(values); if (flat.Length > 0) GL.Uniform4(loc, flat.Length / 4, flat); return s; }

        public static Shader Set(this Shader s, string name, Color4 c)
        { if (TryLoc(s, name, out int loc)) GL.Uniform4(loc, c.R, c.G, c.B, c.A); return s; }
        public static Shader Set(this Shader s, string name, IEnumerable<Color4> values)
        { if (!TryLoc(s, name, out int loc)) return s; float[] flat = Flatten(values); if (flat.Length > 0) GL.Uniform4(loc, flat.Length / 4, flat); return s; }

        public static Shader Set(this Shader s, string name, Quaternion q)
        { Vector3 e = ToEulerXYZ_Radians(q); if (TryLoc(s, name, out int loc)) GL.Uniform3(loc, e.X, e.Y, e.Z); return s; }
        public static Shader Set(this Shader s, string name, Quaterniond q)
        { Vector3d e = ToEulerXYZ_Radians(q); if (TryLoc(s, name, out int loc)) GL.Uniform3(loc, (float)e.X, (float)e.Y, (float)e.Z); return s; }
        public static Shader Set(this Shader s, string name, IEnumerable<Quaternion> qs)
        { if (!TryLoc(s, name, out int loc)) return s; float[] flat = Flatten(qs.Select(ToEulerXYZ_Radians)); if (flat.Length > 0) GL.Uniform3(loc, flat.Length / 3, flat); return s; }
        public static Shader Set(this Shader s, string name, IEnumerable<Quaterniond> qs)
        { if (!TryLoc(s, name, out int loc)) return s; double[] flat = Flatten(qs.Select(ToEulerXYZ_Radians)); if (flat.Length > 0) GL.Uniform3(loc, flat.Length / 3, flat); return s; }

        public static Shader Set(this Shader s, string name, Matrix2 m)
        { if (TryLoc(s, name, out int loc)) GL.UniformMatrix2(loc, false, ref m); return s; }
        public static Shader Set(this Shader s, string name, Matrix3 m)
        { if (TryLoc(s, name, out int loc)) GL.UniformMatrix3(loc, false, ref m); return s; }
        public static Shader Set(this Shader s, string name, Matrix4 m)
        { if (TryLoc(s, name, out int loc)) GL.UniformMatrix4(loc, false, ref m); return s; }

        public static Shader Set(this Shader s, string name, IEnumerable<Matrix2> mats)
        { if (!TryLoc(s, name, out int loc)) return s; float[] flat = Flatten(mats); if (flat.Length > 0) GL.UniformMatrix2(loc, flat.Length / 4, false, flat); return s; }
        public static Shader Set(this Shader s, string name, IEnumerable<Matrix3> mats)
        { if (!TryLoc(s, name, out int loc)) return s; float[] flat = Flatten(mats); if (flat.Length > 0) GL.UniformMatrix3(loc, flat.Length / 9, false, flat); return s; }
        public static Shader Set(this Shader s, string name, IEnumerable<Matrix4> mats)
        { if (!TryLoc(s, name, out int loc)) return s; float[] flat = Flatten(mats); if (flat.Length > 0) GL.UniformMatrix4(loc, flat.Length / 16, false, flat); return s; }

        public static Shader Set(this Shader s, string name, Matrix2d m)
        { if (TryLoc(s, name, out int loc)) GL.UniformMatrix2(loc, false, ref m); return s; }
        public static Shader Set(this Shader s, string name, Matrix3d m)
        { if (TryLoc(s, name, out int loc)) GL.UniformMatrix3(loc, false, ref m); return s; }
        public static Shader Set(this Shader s, string name, Matrix4d m)
        { if (TryLoc(s, name, out int loc)) GL.UniformMatrix4(loc, false, ref m); return s; }

        public static Shader Set(this Shader s, string name, IEnumerable<Matrix2d> mats)
        { if (!TryLoc(s, name, out int loc)) return s; double[] flat = Flatten(mats); if (flat.Length > 0) GL.UniformMatrix2(loc, flat.Length / 4, false, flat); return s; }
        public static Shader Set(this Shader s, string name, IEnumerable<Matrix3d> mats)
        { if (!TryLoc(s, name, out int loc)) return s; double[] flat = Flatten(mats); if (flat.Length > 0) GL.UniformMatrix3(loc, flat.Length / 9, false, flat); return s; }
        public static Shader Set(this Shader s, string name, IEnumerable<Matrix4d> mats)
        { if (!TryLoc(s, name, out int loc)) return s; double[] flat = Flatten(mats); if (flat.Length > 0) GL.UniformMatrix4(loc, flat.Length / 16, false, flat); return s; }

        public static Shader Set(this Shader s, string name, (float x, float y) v) => s.Set(name, new Vector2(v.x, v.y));
        public static Shader Set(this Shader s, string name, (float x, float y, float z) v) => s.Set(name, new Vector3(v.x, v.y, v.z));
        public static Shader Set(this Shader s, string name, (float x, float y, float z, float w) v) => s.Set(name, new Vector4(v.x, v.y, v.z, v.w));
        public static Shader Set(this Shader s, string name, (int x, int y) v) => s.Set(name, new Vector2i(v.x, v.y));
        public static Shader Set(this Shader s, string name, (int x, int y, int z) v) => s.Set(name, new Vector3i(v.x, v.y, v.z));
        public static Shader Set(this Shader s, string name, (int x, int y, int z, int w) v) => s.Set(name, new Vector4i(v.x, v.y, v.z, v.w));

        public static Vector3 ToEulerXYZ_Radians(Quaternion q)
        {
            q = q.Normalized();
            double sinr_cosp = 2.0 * (q.W * q.X + q.Y * q.Z);
            double cosr_cosp = 1.0 - 2.0 * (q.X * q.X + q.Y * q.Y);
            double x = Math.Atan2(sinr_cosp, cosr_cosp);
            double sinp = 2.0 * (q.W * q.Y - q.Z * q.X);
            double y = Math.Abs(sinp) >= 1.0 ? Math.CopySign(Math.PI / 2.0, sinp) : Math.Asin(sinp);
            double siny_cosp = 2.0 * (q.W * q.Z + q.X * q.Y);
            double cosy_cosp = 1.0 - 2.0 * (q.Y * q.Y + q.Z * q.Z);
            double z = Math.Atan2(siny_cosp, cosy_cosp);
            return new Vector3((float)x, (float)y, (float)z);
        }

        public static Vector3d ToEulerXYZ_Radians(Quaterniond q)
        {
            q = q.Normalized();
            double sinr_cosp = 2.0 * (q.W * q.X + q.Y * q.Z);
            double cosr_cosp = 1.0 - 2.0 * (q.X * q.X + q.Y * q.Y);
            double x = Math.Atan2(sinr_cosp, cosr_cosp);
            double sinp = 2.0 * (q.W * q.Y - q.Z * q.X);
            double y = Math.Abs(sinp) >= 1.0 ? Math.CopySign(Math.PI / 2.0, sinp) : Math.Asin(sinp);
            double siny_cosp = 2.0 * (q.W * q.Z + q.X * q.Y);
            double cosy_cosp = 1.0 - 2.0 * (q.Y * q.Y + q.Z * q.Z);
            double z = Math.Atan2(siny_cosp, cosy_cosp);
            return new Vector3d(x, y, z);
        }

        private static float[] Flatten(IEnumerable<Vector2> v)
        { Vector2[] a = v as Vector2[] ?? v.ToArray(); float[] r = new float[a.Length * 2]; for (int i = 0, j = 0; i < a.Length; i++) { r[j++] = a[i].X; r[j++] = a[i].Y; } return r; }
        private static int[] Flatten(IEnumerable<Vector2i> v)
        { Vector2i[] a = v as Vector2i[] ?? v.ToArray(); int[] r = new int[a.Length * 2]; for (int i = 0, j = 0; i < a.Length; i++) { r[j++] = a[i].X; r[j++] = a[i].Y; } return r; }
        private static double[] Flatten(IEnumerable<Vector2d> v)
        { Vector2d[] a = v as Vector2d[] ?? v.ToArray(); double[] r = new double[a.Length * 2]; for (int i = 0, j = 0; i < a.Length; i++) { r[j++] = a[i].X; r[j++] = a[i].Y; } return r; }

        private static float[] Flatten(IEnumerable<Vector3> v)
        { Vector3[] a = v as Vector3[] ?? v.ToArray(); float[] r = new float[a.Length * 3]; for (int i = 0, j = 0; i < a.Length; i++) { r[j++] = a[i].X; r[j++] = a[i].Y; r[j++] = a[i].Z; } return r; }
        private static int[] Flatten(IEnumerable<Vector3i> v)
        { Vector3i[] a = v as Vector3i[] ?? v.ToArray(); int[] r = new int[a.Length * 3]; for (int i = 0, j = 0; i < a.Length; i++) { r[j++] = a[i].X; r[j++] = a[i].Y; r[j++] = a[i].Z; } return r; }
        private static double[] Flatten(IEnumerable<Vector3d> v)
        { Vector3d[] a = v as Vector3d[] ?? v.ToArray(); double[] r = new double[a.Length * 3]; for (int i = 0, j = 0; i < a.Length; i++) { r[j++] = a[i].X; r[j++] = a[i].Y; r[j++] = a[i].Z; } return r; }

        private static float[] Flatten(IEnumerable<Vector4> v)
        { Vector4[] a = v as Vector4[] ?? v.ToArray(); float[] r = new float[a.Length * 4]; for (int i = 0, j = 0; i < a.Length; i++) { r[j++] = a[i].X; r[j++] = a[i].Y; r[j++] = a[i].Z; r[j++] = a[i].W; } return r; }
        private static int[] Flatten(IEnumerable<Vector4i> v)
        { Vector4i[] a = v as Vector4i[] ?? v.ToArray(); int[] r = new int[a.Length * 4]; for (int i = 0, j = 0; i < a.Length; i++) { r[j++] = a[i].X; r[j++] = a[i].Y; r[j++] = a[i].Z; r[j++] = a[i].W; } return r; }
        private static double[] Flatten(IEnumerable<Vector4d> v)
        { Vector4d[] a = v as Vector4d[] ?? v.ToArray(); double[] r = new double[a.Length * 4]; for (int i = 0, j = 0; i < a.Length; i++) { r[j++] = a[i].X; r[j++] = a[i].Y; r[j++] = a[i].Z; r[j++] = a[i].W; } return r; }

        private static float[] Flatten(IEnumerable<Color4> v)
        { Color4[] a = v as Color4[] ?? v.ToArray(); float[] r = new float[a.Length * 4]; for (int i = 0, j = 0; i < a.Length; i++) { r[j++] = a[i].R; r[j++] = a[i].G; r[j++] = a[i].B; r[j++] = a[i].A; } return r; }

        private static float[] Flatten(IEnumerable<Matrix2> v)
        { Matrix2[] a = v as Matrix2[] ?? v.ToArray(); float[] r = new float[a.Length * 4]; int k = 0; for (int i = 0; i < a.Length; i++) { r[k++] = a[i].Row0.X; r[k++] = a[i].Row1.X; r[k++] = a[i].Row0.Y; r[k++] = a[i].Row1.Y; } return r; }
        private static float[] Flatten(IEnumerable<Matrix3> v)
        { Matrix3[] a = v as Matrix3[] ?? v.ToArray(); float[] r = new float[a.Length * 9]; int k = 0; for (int i = 0; i < a.Length; i++) { r[k++] = a[i].Row0.X; r[k++] = a[i].Row1.X; r[k++] = a[i].Row2.X; r[k++] = a[i].Row0.Y; r[k++] = a[i].Row1.Y; r[k++] = a[i].Row2.Y; r[k++] = a[i].Row0.Z; r[k++] = a[i].Row1.Z; r[k++] = a[i].Row2.Z; } return r; }
        private static float[] Flatten(IEnumerable<Matrix4> v)
        { Matrix4[] a = v as Matrix4[] ?? v.ToArray(); float[] r = new float[a.Length * 16]; int k = 0; for (int i = 0; i < a.Length; i++) { r[k++] = a[i].Row0.X; r[k++] = a[i].Row1.X; r[k++] = a[i].Row2.X; r[k++] = a[i].Row3.X; r[k++] = a[i].Row0.Y; r[k++] = a[i].Row1.Y; r[k++] = a[i].Row2.Y; r[k++] = a[i].Row3.Y; r[k++] = a[i].Row0.Z; r[k++] = a[i].Row1.Z; r[k++] = a[i].Row2.Z; r[k++] = a[i].Row3.Z; r[k++] = a[i].Row0.W; r[k++] = a[i].Row1.W; r[k++] = a[i].Row2.W; r[k++] = a[i].Row3.W; } return r; }

        private static double[] Flatten(IEnumerable<Matrix2d> v)
        { Matrix2d[] a = v as Matrix2d[] ?? v.ToArray(); double[] r = new double[a.Length * 4]; int k = 0; for (int i = 0; i < a.Length; i++) { r[k++] = a[i].Row0.X; r[k++] = a[i].Row1.X; r[k++] = a[i].Row0.Y; r[k++] = a[i].Row1.Y; } return r; }
        private static double[] Flatten(IEnumerable<Matrix3d> v)
        { Matrix3d[] a = v as Matrix3d[] ?? v.ToArray(); double[] r = new double[a.Length * 9]; int k = 0; for (int i = 0; i < a.Length; i++) { r[k++] = a[i].Row0.X; r[k++] = a[i].Row1.X; r[k++] = a[i].Row2.X; r[k++] = a[i].Row0.Y; r[k++] = a[i].Row1.Y; r[k++] = a[i].Row2.Y; r[k++] = a[i].Row0.Z; r[k++] = a[i].Row1.Z; r[k++] = a[i].Row2.Z; } return r; }
        private static double[] Flatten(IEnumerable<Matrix4d> v)
        { Matrix4d[] a = v as Matrix4d[] ?? v.ToArray(); double[] r = new double[a.Length * 16]; int k = 0; for (int i = 0; i < a.Length; i++) { r[k++] = a[i].Row0.X; r[k++] = a[i].Row1.X; r[k++] = a[i].Row2.X; r[k++] = a[i].Row3.X; r[k++] = a[i].Row0.Y; r[k++] = a[i].Row1.Y; r[k++] = a[i].Row2.Y; r[k++] = a[i].Row3.Y; r[k++] = a[i].Row0.Z; r[k++] = a[i].Row1.Z; r[k++] = a[i].Row2.Z; r[k++] = a[i].Row3.Z; r[k++] = a[i].Row0.W; r[k++] = a[i].Row1.W; r[k++] = a[i].Row2.W; r[k++] = a[i].Row3.W; } return r; }
    }
}
