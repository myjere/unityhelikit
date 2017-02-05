﻿using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using HeliSharp;
using MathNet.Numerics.LinearAlgebra;

public abstract class Helicopter : MonoBehaviour
{

    public bool airStart = true;

    public Text debugText;

    public abstract HeliSharp.Helicopter model { get; }
    protected Rigidbody body;

    // "Relay" controls to dynamics model
    public float Throttle {
        get { return model is HeliSharp.SingleMainRotorHelicopter ? (float)((HeliSharp.SingleMainRotorHelicopter)model).Engine.throttle : 0; }
        set { if (model is HeliSharp.SingleMainRotorHelicopter) ((HeliSharp.SingleMainRotorHelicopter)model).Engine.throttle = value; }
    }
    public bool TrimControl {
        get { return model.FCS.trimControl; }
        set { model.FCS.trimControl = value; }
    }
    public float Collective {
        get { return (float)model.Collective; }
        set { model.Collective = value; }
    }
    public float LongCyclic {
        get { return (float)model.LongCyclic; }
        set { model.LongCyclic = value; }
    }
    public float LatCyclic {
        get { return (float)model.LatCyclic; }
        set { model.LatCyclic = value; }
    }
    public float Pedal {
        get { return (float)model.Pedal; }
        set { model.Pedal = value; }
    }
    public float LeftBrake { get; set; }
    public float RightBrake { get; set; }

    public virtual void FindComponents() {
        body = GetComponent<Rigidbody>();
        if (body == null) {
            body = gameObject.AddComponent<Rigidbody>();
            body.mass = (float)model.Mass;
            body.drag = body.angularDrag = 0f;
        }
        Transform centerOfMassTransform = transform.FindChild("CenterOfMass");
        if (centerOfMassTransform != null) body.centerOfMass = centerOfMassTransform.localPosition;
    }

    public virtual void Trim(bool initial)
    {
        Debug.Log("Trim" + (initial ? " initial" : ""));
        model.Rotation = body.rotation.FromUnity();
        if (initial) {
            model.RollAngle = 0;
            model.PitchAngle = 0;
            model.AngularVelocity = Vector<double>.Build.Dense(3);
            model.TrimInit();
        }

        // Trim for equilibrium
        try {
            model.Trim();
        } catch (HeliSharp.TrimmerException e) {
            Debug.LogException(e);
            enabled = false;
            if (debugText != null) debugText.text = "TRIM FAIL";
            return;
        }

        if (!initial || airStart) body.rotation = model.Rotation.ToUnity ();
    }

    public abstract void ToggleEngine();

    public virtual void FixedUpdate() {
        if (body == null) return;

        // Set velocities and attitues from rigid body simulation
        model.Velocity = transform.InverseTransformDirection(body.velocity).FromUnity();
        model.AngularVelocity = -transform.InverseTransformDirection(body.angularVelocity).FromUnity();
        model.Translation = transform.position.FromUnity();
        model.Rotation = transform.rotation.FromUnity();

        // Set height from ray trace (for ground effect)
        model.Height = GetHeight() ?? 999;

        if (model.Collective < -1) model.Collective = -1;

        if (debugText != null) {
            string text = "";
            text += "COLL " + model.Collective.ToStr() + " LONG " + model.LongCyclic.ToStr() + " LAT " + model.LatCyclic.ToStr() + " PED " + model.Pedal.ToStr() + "\n";
            text += "ATT x " + Mathf.Round((float)model.Attitude.x() * 180f / Mathf.PI) + " y " + Mathf.Round((float)model.Attitude.y() * 180f / Mathf.PI) + " z " + Mathf.Round((float)model.Attitude.z() * 180f / Mathf.PI) + "\n";
            //text += "PITCH " + (model.PitchAngle * 180.0 / Mathf.PI).ToStr() + " ROLL " + (model.RollAngle * 180.0 / Mathf.PI).ToStr() + "\n";
            text += "ALT " + Mathf.Round(transform.position.y) + "m HEIGHT " + Mathf.Round((float)model.Height) + "m\n";
            text += "SPEED " + Mathf.Round((float)model.Velocity.x() * 1.9438f) + "kts LAT " + Mathf.Round((float)model.Velocity.y() * 1.9438f) + " kts VERT " + Mathf.Round(body.velocity.y * 197f) + " fpm\n";
            //text += "VEL " + (int)model.Velocity.x() + " " + (int)model.Velocity.y() + " " + (int)model.Velocity.z() + "\n";
            text += "AVEL " + (int)(model.AngularVelocity.x() * 100) + " " + (int)(model.AngularVelocity.y() * 100) + " " + (int)(model.AngularVelocity.z() * 100) + "\n";
            //text += "F " + (int)model.Force.x() + " " + (int)model.Force.y() + " " + (int)model.Force.z() + "\n";
            //text += "M " + (int)model.Torque.x() + " " + (int)model.Torque.y() + " " + (int)model.Torque.z() + "\n";
            //text += "M/R F " + (int)mainRotor.Force.x() + " " + (int)mainRotor.Force.y() + " " + (int)mainRotor.Force.z() + "\n";
            //text += "M/R M " + (int)mainRotor.Torque.x() + " " + (int)mainRotor.Torque.y() + " " + (int)mainRotor.Torque.z() + "\n";
            //text += "H/S Fz " + (int)horizontalStabilizer.Force.z () + " V/S Fz " + (int)verticalStabilizer.Force.z () + "\n";
            //text += "FUSE Mz " + (int)fuselage.Torque.z () + "\n";
            //text += "uF " + (int)force.x + " " + (int)force.y + " " + (int)force.z + "\n";
            //text += "uM " + (int)torque.x + " " + (int)torque.y + " " + (int)torque.z + "\n";
            if (LeftBrake > 0.01f || RightBrake > 0.01f) text += "BRAKE\n";
            debugText.text = text;
        }

        // Update dynamics
        model.Update(Time.fixedDeltaTime);

        // Set force and torque/moment in rigid body simulation
        Vector3 force = model.Force.ToUnity();
        Vector3 torque = -model.Torque.ToUnity(); // minus because Unity uses a left-hand coordinate system
        body.AddRelativeForce(force);
        body.AddRelativeTorque(torque);

    }

    public float? GetHeight() {
        RaycastHit hit;
        if (Physics.Raycast(transform.position, new Vector3(0,-1,0), out hit, 1000f)) {
            return transform.position.y - hit.point.y;
        }
        return null;
    }

    public abstract void ParametrizeUnityFromModel();

    public abstract void ParametrizeModelsFromUnity();

    protected void DebugDrawRotor(Transform transform, Rotor rotor, int numSegments) {
        for (int i = 0; i < numSegments; i++) {
            float step = 360f / numSegments;
            float a = i * step;
            Vector3 p1 = new Vector3((float)rotor.R * Mathf.Cos(a * Mathf.PI / 180f), (float)rotor.R * Mathf.Sin((float)rotor.beta_0), (float)rotor.R * Mathf.Sin(a * Mathf.PI / 180f));
            Vector3 p2 = new Vector3((float)rotor.R * Mathf.Cos((a + step) * Mathf.PI / 180f), (float)rotor.R * Mathf.Sin((float)rotor.beta_0), (float)rotor.R * Mathf.Sin((a + step) * Mathf.PI / 180f));
            Debug.DrawLine(transform.TransformPoint(p1), transform.TransformPoint(p2), Color.gray);
            if (i % (numSegments / rotor.Nb) == 0)
                Debug.DrawLine(transform.position, transform.TransformPoint(p1), Color.gray);

        }
    }

    protected void DebugDrawStabilizer(Transform transform, Stabilizer stabilizer) {
        Vector3 tl = new Vector3((float)-stabilizer.span / 2, 0, (float)stabilizer.chord / 2);
        Vector3 tr = new Vector3((float)stabilizer.span / 2, 0, (float)stabilizer.chord / 2);
        Vector3 br = new Vector3((float)stabilizer.span / 2, 0, (float)-stabilizer.chord / 2);
        Vector3 bl = new Vector3((float)-stabilizer.span / 2, 0, (float)-stabilizer.chord / 2);
        Debug.DrawLine(transform.TransformPoint(tl), transform.TransformPoint(tr), Color.gray);
        Debug.DrawLine(transform.TransformPoint(tr), transform.TransformPoint(br), Color.gray);
        Debug.DrawLine(transform.TransformPoint(br), transform.TransformPoint(bl), Color.gray);
        Debug.DrawLine(transform.TransformPoint(bl), transform.TransformPoint(tl), Color.gray);
    }

}
