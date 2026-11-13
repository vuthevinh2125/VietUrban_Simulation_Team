using UnityEngine;

/// <summary>
/// Sinh vật không phải người ("quỷ đói") dựng bằng code: cao ~2.3 m, gầy trơ xương sườn, da xám tái, tay chân dài ngoằng,
/// ngón tay như móng vuốt, đầu dài, hốc mắt đen sâu, hàm răng nhọn há rộng, máu đỏ quanh miệng / ngực / bàn tay.
/// Khung xương là các Transform lồng nhau; tư thế đặt bằng góc khớp, chuyển động tự tính (đứng gù lắc lư, bò bốn chân,
/// vươn tay chồm tới, treo ngược trên trần), giật cục ngẫu nhiên ở đầu.
/// </summary>
public class HorrorCreature : MonoBehaviour
{
    public enum Mode { Stand, Walk, Crawl, Reach, Hang }
    public Mode mode = Mode.Stand;
    [Range(0f, 1f)] public float jawOpen;
    public float gait = 1f;          // tốc độ bước khi bò
    public float twitch = 0.6f;      // giật đầu
    public float headTwistTarget, headTiltTarget;   // vặn đầu (độ) - vặn ngược 180 độ khi "quay lại nhìn"
    public float headSpeed = 600f;
    private float headTwist, headTilt;

    private Transform hips, spine, neck, head, jaw, shL, shR, elL, elR, wrL, wrR, thL, thR, knL, knR;
    private float phase, nextTwitch, twitchUntil, jaw01;
    private Vector3 twitchAng;

    private static Material mat => VPalette.Material;
    private static readonly Color Skin = new Color(0.56f, 0.58f, 0.55f), SkinDark = new Color(0.34f, 0.35f, 0.33f),
        Gore = new Color(0.42f, 0.02f, 0.02f), Hole = new Color(0.015f, 0.01f, 0.01f), Teeth = new Color(0.86f, 0.82f, 0.68f);

    private static Mesh mTorso, mNeck, mSkull, mJaw, mUpper, mLower, mHand, mThigh, mShin, mFoot;

    public static HorrorCreature Create(Transform parent, Vector3 pos, Quaternion rot)
    {
        BuildMeshes();
        var g = new GameObject("HorrorCreature");
        g.transform.SetParent(parent, false);
        g.transform.SetPositionAndRotation(pos, rot);
        var c = g.AddComponent<HorrorCreature>();
        c.Rig();
        return c;
    }

    private static Mesh Make(string name, System.Action<VMeshBuilder> build)
    {
        var mb = new VMeshBuilder();
        build(mb);
        Mesh m = mb.ToMesh(RealModels.MeshPrefix + "_Creature" + name);
        m.hideFlags = HideFlags.DontSave;
        return m;
    }

    private static Matrix4x4 M(float x, float y, float z, float sx, float sy, float sz, float rx = 0f, float ry = 0f, float rz = 0f) =>
        Matrix4x4.TRS(new Vector3(x, y, z), Quaternion.Euler(rx, ry, rz), new Vector3(sx, sy, sz));

    private static void BuildMeshes()
    {
        if (mTorso != null) return;
        mTorso = Make("Torso", b =>
        {
            b.Add(VShape.RoundBox(0.45f, 12), M(0f, 0.38f, 0f, 0.34f, 0.5f, 0.2f), Skin);                  // lồng ngực
            for (int i = 0; i < 6; i++)                                                                     // xương sườn nổi
                b.Add(VShape.Torus(0.08f, 16), M(0f, 0.2f + i * 0.065f, 0.005f, 0.35f, 0.04f, 0.22f), SkinDark);
            b.Add(VShape.RoundBox(0.5f, 10), M(0f, 0.06f, 0f, 0.2f, 0.22f, 0.14f), SkinDark);               // bụng hóp
            b.Add(VShape.RoundBox(0.5f, 10), M(0.02f, 0.46f, 0.1f, 0.2f, 0.26f, 0.02f), Gore);              // máu chảy loang ngực
            b.Add(VShape.Cylinder(8), M(0.05f, 0.24f, 0.105f, 0.03f, 0.3f, 0.01f), Gore);
            b.Add(VShape.RoundBox(0.4f, 10), M(0f, -0.06f, 0f, 0.28f, 0.14f, 0.17f), SkinDark);             // hông
        });
        mNeck = Make("Neck", b => b.Add(VShape.Cylinder(10), M(0f, 0.17f, 0f, 0.07f, 0.36f, 0.07f), SkinDark));
        mSkull = Make("Skull", b =>
        {
            b.Add(VShape.Sphere(16), M(0f, 0.15f, 0.0f, 0.2f, 0.32f, 0.25f), Skin);                         // sọ dài
            for (int s = -1; s <= 1; s += 2)
            {
                b.Add(VShape.Sphere(10), M(s * 0.052f, 0.18f, 0.105f, 0.065f, 0.07f, 0.05f), Hole);          // hốc mắt đen
                b.Add(VShape.Cylinder(6), M(s * 0.05f, 0.11f, 0.118f, 0.012f, 0.09f, 0.006f), Gore);         // máu chảy từ mắt
            }
            b.Add(VShape.Sphere(8), M(0f, 0.11f, 0.125f, 0.03f, 0.02f, 0.02f), Hole);                        // lỗ mũi
            for (int k = 0; k < 9; k++)                                                                      // răng trên nhọn
                b.Add(VShape.Cone, M(-0.064f + k * 0.016f, 0.045f, 0.1f - Mathf.Abs(k - 4) * 0.006f, 0.013f, 0.05f, 0.013f, 180f), Teeth);
            b.Add(VShape.RoundBox(0.3f, 8), M(0f, 0.07f, 0.09f, 0.16f, 0.03f, 0.06f), Gore);                 // lợi đỏ
        });
        mJaw = Make("Jaw", b =>
        {
            b.Add(VShape.RoundBox(0.4f, 10), M(0f, -0.03f, 0.05f, 0.15f, 0.05f, 0.14f), Skin);
            for (int k = 0; k < 8; k++)
                b.Add(VShape.Cone, M(-0.056f + k * 0.016f, 0.0f, 0.1f - Mathf.Abs(k - 3.5f) * 0.006f, 0.012f, 0.045f, 0.012f), Teeth);
            b.Add(VShape.RoundBox(0.4f, 8), M(0f, -0.005f, 0.06f, 0.11f, 0.02f, 0.1f), Gore);                // lưỡi / máu
            b.Add(VShape.Cylinder(6), M(0.02f, -0.12f, 0.11f, 0.014f, 0.16f, 0.01f), Gore);                  // dãi máu
        });
        mUpper = Make("Upper", b => { b.Add(VShape.Cylinder(10), M(0f, -0.3f, 0f, 0.075f, 0.6f, 0.075f), Skin); b.Add(VShape.Sphere(10), M(0f, -0.6f, 0f, 0.085f, 0.085f, 0.085f), SkinDark); });
        mLower = Make("Lower", b => { b.Add(VShape.Cylinder(10), M(0f, -0.32f, 0f, 0.06f, 0.64f, 0.06f), Skin); b.Add(VShape.Cylinder(8), M(0f, -0.5f, 0.02f, 0.064f, 0.22f, 0.064f), Gore); });
        mHand = Make("Hand", b =>
        {
            b.Add(VShape.RoundBox(0.4f, 8), M(0f, -0.06f, 0f, 0.1f, 0.12f, 0.035f), Gore);
            for (int f = 0; f < 4; f++)
            {
                float x = -0.04f + f * 0.027f;
                b.Add(VShape.Cylinder(6), M(x, -0.2f, 0.02f, 0.016f, 0.24f, 0.016f, 12f), SkinDark);         // ngón dài
                b.Add(VShape.Cone, M(x, -0.34f, 0.045f, 0.014f, 0.06f, 0.014f, 192f), Hole);                // móng vuốt
            }
            b.Add(VShape.Cylinder(6), M(0.06f, -0.1f, 0.02f, 0.016f, 0.16f, 0.016f, 0f, 0f, 40f), SkinDark); // ngón cái
        });
        mThigh = Make("Thigh", b => b.Add(VShape.Cylinder(10), M(0f, -0.3f, 0f, 0.085f, 0.62f, 0.085f), Skin));
        mShin = Make("Shin", b => { b.Add(VShape.Cylinder(10), M(0f, -0.3f, 0f, 0.065f, 0.6f, 0.065f), Skin); b.Add(VShape.Sphere(8), M(0f, 0f, 0f, 0.09f, 0.09f, 0.09f), SkinDark); });
        mFoot = Make("Foot", b =>
        {
            b.Add(VShape.RoundBox(0.4f, 8), M(0f, -0.02f, 0.08f, 0.08f, 0.05f, 0.24f), SkinDark);
            for (int f = 0; f < 3; f++) b.Add(VShape.Cone, M(-0.025f + f * 0.025f, -0.02f, 0.22f, 0.014f, 0.06f, 0.014f, 90f), Hole);
        });
    }

    private Transform Joint(string name, Transform parent, Vector3 local, Mesh mesh)
    {
        var g = new GameObject(name);
        g.transform.SetParent(parent, false);
        g.transform.localPosition = local;
        if (mesh != null)
        {
            g.AddComponent<MeshFilter>().sharedMesh = mesh;
            var r = g.AddComponent<MeshRenderer>();
            r.sharedMaterial = mat;
        }
        return g.transform;
    }

    private void Rig()
    {
        hips = Joint("Hips", transform, new Vector3(0f, 1.05f, 0f), null);
        spine = Joint("Spine", hips, Vector3.zero, mTorso);
        neck = Joint("Neck", spine, new Vector3(0f, 0.62f, 0.03f), mNeck);
        head = Joint("Head", neck, new Vector3(0f, 0.34f, 0f), mSkull);
        jaw = Joint("Jaw", head, new Vector3(0f, 0.05f, 0.02f), mJaw);
        shL = Joint("ShoulderL", spine, new Vector3(-0.2f, 0.58f, 0f), mUpper);
        shR = Joint("ShoulderR", spine, new Vector3(0.2f, 0.58f, 0f), mUpper);
        elL = Joint("ElbowL", shL, new Vector3(0f, -0.6f, 0f), mLower);
        elR = Joint("ElbowR", shR, new Vector3(0f, -0.6f, 0f), mLower);
        wrL = Joint("WristL", elL, new Vector3(0f, -0.64f, 0f), mHand);
        wrR = Joint("WristR", elR, new Vector3(0f, -0.64f, 0f), mHand);
        thL = Joint("ThighL", hips, new Vector3(-0.11f, -0.04f, 0f), mThigh);
        thR = Joint("ThighR", hips, new Vector3(0.11f, -0.04f, 0f), mThigh);
        knL = Joint("KneeL", thL, new Vector3(0f, -0.62f, 0f), mShin);
        knR = Joint("KneeR", thR, new Vector3(0f, -0.62f, 0f), mShin);
        Joint("FootL", knL, new Vector3(0f, -0.6f, 0f), mFoot);
        Joint("FootR", knR, new Vector3(0f, -0.6f, 0f), mFoot);
        Apply(0f);
    }

    /// <summary>Há miệng thét (âm 3D tại đầu).</summary>
    public void Scream(float volume = 1f)
    {
        jawOpen = 1f;
        if (head != null) HorrorAudio.Play3D(HorrorAudio.Scream(), head.position, volume, false, 4f, 60f);
    }

    public Vector3 HeadPos => head != null ? head.position + head.up * 0.15f : transform.position + Vector3.up * 2f;

    public void SetLayer(int layer)
    {
        if (layer < 0) return;
        foreach (Transform t in GetComponentsInChildren<Transform>(true)) t.gameObject.layer = layer;
    }

    void Update() => Apply(Time.deltaTime);

    private static void R(Transform t, float x, float y, float z) { if (t != null) t.localRotation = Quaternion.Euler(x, y, z); }

    private void Apply(float dt)
    {
        phase += dt * (mode == Mode.Crawl ? 5f * gait : mode == Mode.Walk ? 3.2f * gait : 1.4f);
        float s = Mathf.Sin(phase), c = Mathf.Cos(phase);
        jaw01 = Mathf.MoveTowards(jaw01, jawOpen, dt * 6f);
        if (twitch > 0f && Time.time >= nextTwitch)
        {
            nextTwitch = Time.time + Random.Range(0.3f, 1.6f) / twitch;
            twitchUntil = Time.time + Random.Range(0.05f, 0.12f);
            twitchAng = new Vector3(Random.Range(-30f, 30f), Random.Range(-40f, 40f), Random.Range(-45f, 45f));
        }
        Vector3 tw = Time.time < twitchUntil ? twitchAng : Vector3.zero;
        R(jaw, 8f + jaw01 * 48f, 0f, 0f);
        float hb = headTwist;
        headTwist = Mathf.MoveTowards(headTwist, headTwistTarget, headSpeed * dt);
        headTilt = Mathf.MoveTowards(headTilt, headTiltTarget, headSpeed * dt);
        if (Mathf.Abs(headTwist) > 120f && Mathf.Abs(hb) <= 120f && head != null) HorrorAudio.Play3D(HorrorAudio.Crack(), head.position, 0.9f, false, 3f, 40f);
        switch (mode)
        {
            case Mode.Walk:    // đi lê từng bước như xác chết: gù, hai tay buông thõng đung đưa, đầu lắc lư
                hips.localPosition = new Vector3(0f, 1.02f + Mathf.Abs(c) * 0.04f, 0f);
                R(spine, 24f + s * 3f, s * 4f, s * 4f);
                R(neck, -12f, 0f, 0f);
                R(head, -14f + tw.x, tw.y, 18f + tw.z);
                R(shL, 6f - s * 14f, 0f, -6f); R(shR, 6f + s * 14f, 0f, 6f);
                R(elL, -14f, 0f, 0f); R(elR, -14f, 0f, 0f);
                R(wrL, -10f, 0f, 0f); R(wrR, -10f, 0f, 0f);
                R(thL, -18f + s * 22f, 0f, 3f); R(thR, -18f - s * 22f, 0f, -3f);
                R(knL, 22f + Mathf.Max(0f, c) * 30f, 0f, 0f); R(knR, 22f + Mathf.Max(0f, -c) * 30f, 0f, 0f);
                break;
            case Mode.Stand:   // gù lưng, tay buông dài quá gối, đầu nghiêng gãy, lắc lư
                hips.localPosition = new Vector3(0f, 1.0f, 0f);
                R(spine, 28f + s * 2f, 0f, s * 3f);
                R(neck, -10f, 0f, 0f);
                R(head, -20f + tw.x, tw.y, 28f + tw.z);
                R(shL, 8f + s * 4f, 0f, -6f); R(shR, 8f - s * 4f, 0f, 6f);
                R(elL, -12f, 0f, 0f); R(elR, -12f, 0f, 0f);
                R(wrL, -10f, 0f, 0f); R(wrR, -10f, 0f, 0f);
                R(thL, -14f, 0f, 3f); R(thR, -14f, 0f, -3f);
                R(knL, 26f, 0f, 0f); R(knR, 26f, 0f, 0f);
                break;
            case Mode.Reach:   // chồm tới, hai tay vươn thẳng, móng xoè, đầu ngửa há mồm
                hips.localPosition = new Vector3(0f, 1.0f, 0f);
                R(spine, 35f, 0f, 0f);
                R(neck, -30f, 0f, 0f);
                R(head, -25f + tw.x * 0.5f, tw.y * 0.5f, tw.z * 0.5f);
                R(shL, -115f + s * 6f, 0f, -12f); R(shR, -115f - s * 6f, 0f, 12f);
                R(elL, -8f, 0f, 0f); R(elR, -8f, 0f, 0f);
                R(wrL, 20f, 0f, 0f); R(wrR, 20f, 0f, 0f);
                R(thL, -30f, 0f, 3f); R(thR, 10f, 0f, -3f);
                R(knL, 40f, 0f, 0f); R(knR, 20f, 0f, 0f);
                break;
            case Mode.Crawl:   // bò bốn chân như nhện: thân nằm ngang, đầu ngóc lên nhìn thẳng
                hips.localPosition = new Vector3(0f, 0.72f, 0f);
                R(spine, 82f, 0f, s * 6f);
                R(neck, -55f, 0f, 0f);
                R(head, -30f + tw.x, tw.y, tw.z);
                R(shL, -70f + s * 28f, 0f, -18f); R(shR, -70f - s * 28f, 0f, 18f);
                R(elL, -30f - Mathf.Max(0f, c) * 30f, 0f, 0f); R(elR, -30f - Mathf.Max(0f, -c) * 30f, 0f, 0f);
                R(wrL, 50f, 0f, 0f); R(wrR, 50f, 0f, 0f);
                R(thL, -70f - s * 25f, 0f, 25f); R(thR, -70f + s * 25f, 0f, -25f);
                R(knL, 100f + Mathf.Max(0f, -c) * 30f, 0f, 0f); R(knR, 100f + Mathf.Max(0f, c) * 30f, 0f, 0f);
                break;
            case Mode.Hang:    // treo ngược trên trần: cả người lộn ngược, tay buông thõng xuống, đầu vặn ngửa nhìn xuống
                hips.localPosition = new Vector3(0f, 1.0f, 0f);
                R(spine, 15f, 0f, s * 2f);
                R(neck, 20f, 0f, 0f);
                R(head, 40f + tw.x, 180f + tw.y, tw.z);
                R(shL, 160f + s * 5f, 0f, -10f); R(shR, 160f - s * 5f, 0f, 10f);
                R(elL, -20f, 0f, 0f); R(elR, -20f, 0f, 0f);
                R(wrL, 0f, 0f, 0f); R(wrR, 0f, 0f, 0f);
                R(thL, 0f, 0f, 6f); R(thR, 0f, 0f, -6f);
                R(knL, 10f, 0f, 0f); R(knR, 10f, 0f, 0f);
                break;
        }
        if (head != null && (Mathf.Abs(headTwist) > 0.01f || Mathf.Abs(headTilt) > 0.01f))
            head.localRotation = head.localRotation * Quaternion.Euler(0f, headTwist, headTilt);
    }

    // ================= Đồ tang (cho "đoàn đưa tang" quỷ) =================
    private static Mesh mRobe, mSkirt, mHood, mBand;

    /// <summary>Khoác đồ tang rách tả tơi, loang máu: áo trùm thân, vạt váy dài, mũ mấn (hood) hoặc khăn tang.</summary>
    public void Mourning(bool hood)
    {
        if (mRobe == null)
        {
            Color w = new Color(0.86f, 0.86f, 0.82f), wd = new Color(0.7f, 0.7f, 0.66f), b = new Color(0.5f, 0.02f, 0.02f), bd = new Color(0.32f, 0.01f, 0.01f);
            mRobe = Make("Robe", m =>
            {
                m.Add(VShape.RoundBox(0.4f, 12), M(0f, 0.36f, 0f, 0.42f, 0.66f, 0.27f), w);
                m.Add(VShape.RoundBox(0.4f, 10), M(0.06f, 0.48f, 0.132f, 0.16f, 0.3f, 0.012f), b);      // máu ướt trước ngực
                m.Add(VShape.Cube, M(0.07f, 0.26f, 0.136f, 0.035f, 0.32f, 0.006f), b);                 // vệt chảy
                m.Add(VShape.Cube, M(-0.08f, 0.3f, 0.136f, 0.025f, 0.22f, 0.006f), bd);
                m.Add(VShape.RoundBox(0.4f, 10), M(-0.1f, 0.2f, -0.13f, 0.14f, 0.2f, 0.012f), bd);     // máu khô sau lưng
            });
            mSkirt = Make("Skirt", m =>
            {
                m.Add(VShape.Cone, M(0f, -0.42f, 0f, 0.62f, 0.9f, 0.5f), wd);
                for (int i = 0; i < 7; i++)                                                             // vạt rách tả tơi
                {
                    float a = i / 7f * Mathf.PI * 2f;
                    m.Add(VShape.Cube, M(Mathf.Sin(a) * 0.27f, -0.9f, Mathf.Cos(a) * 0.22f, 0.1f, 0.22f, 0.01f, 0f, a * Mathf.Rad2Deg, 0f), i % 3 == 0 ? b : wd);
                }
                m.Add(VShape.RoundBox(0.4f, 8), M(0.12f, -0.6f, 0.2f, 0.14f, 0.3f, 0.012f), b);
            });
            mHood = Make("Hood", m =>
            {
                m.Add(VShape.Cone, M(0f, 0.3f, -0.02f, 0.3f, 0.42f, 0.32f), w);                           // mũ mấn
                m.Add(VShape.RoundBox(0.3f, 8), M(0f, 0.05f, -0.13f, 0.28f, 0.42f, 0.04f), w);           // vạt sau
                m.Add(VShape.RoundBox(0.3f, 8), M(0.05f, 0.3f, 0.14f, 0.07f, 0.12f, 0.01f), b);
            });
            mBand = Make("Band", m =>
            {
                m.Add(VShape.Torus(0.16f, 18), M(0f, 0.2f, 0.01f, 0.24f, 0.24f, 0.28f), w);
                m.Add(VShape.RoundBox(0.3f, 8), M(0f, 0.06f, -0.14f, 0.05f, 0.3f, 0.01f, -10f), w);
                m.Add(VShape.Cube, M(0.04f, 0.2f, 0.15f, 0.05f, 0.03f, 0.01f), b);
            });
        }
        Joint("Robe", spine, Vector3.zero, mRobe);
        Joint("Skirt", hips, Vector3.zero, mSkirt);
        Joint(hood ? "Hood" : "Band", head, Vector3.zero, hood ? mHood : mBand);
    }

}
