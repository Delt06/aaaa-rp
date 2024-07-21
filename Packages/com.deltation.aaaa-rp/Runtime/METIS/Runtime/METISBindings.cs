using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;

namespace DELTation.AAAARP.METIS.Runtime
{
    internal static unsafe class METISBindings
    {
        // ReSharper disable once InconsistentNaming
        public const int METIS_NOPTIONS = 40;

        private const string DLL = "metis.dll";
        private const CharSet CharSet = System.Runtime.InteropServices.CharSet.Auto;
        private const CallingConvention CallingConvention = System.Runtime.InteropServices.CallingConvention.Cdecl;

        /// <summary>
        ///     Is used to partition a graph into k parts using either multilevel recursive bisection or multilevel k-way
        ///     partitioning
        ///     https://www.lrz.de/services/software/mathematik/metis/metis_5_0.pdf
        /// </summary>
        /// <param name="nvtxs">The number of vertices in the graph.</param>
        /// <param name="ncon">The number of balancing constraints. It should be at least 1.</param>
        /// <param name="xadj">The adjacency structure of the graph as described in Section 5.5.</param>
        /// <param name="adjncy">The adjacency structure of the graph as described in Section 5.5.</param>
        /// <param name="vwgt">(NULL) The weights of the vertices as described in Section 5.5.</param>
        /// <param name="vsize">
        ///     (NULL) The size of the vertices for computing the total communication volume as described in
        ///     Section 5.7.
        /// </param>
        /// <param name="adjwgt">(NULL) Information about the weights of the vertices and edges as described in Section 5.1.</param>
        /// <param name="nparts">The number of parts to partition the graph.</param>
        /// <param name="tpwgts">
        ///     (NULL) This is an array of size nparts×ncon that specifies the desired weight for each partition and constraint.
        ///     The target partition weight for the ith partition and jth constraint is specified at tpwgts[i*ncon+j]
        ///     (the numbering for both partitions and constraints starts from 0). For each constraint, the sum of the
        ///     tpwgts[] entries must be 1.0 (i.e., Pi tpwgts[i ∗ ncon + j] = 1:0).
        ///     A NULL value can be passed to indicate that the graph should be equally divided among the partitions.
        /// </param>
        /// <param name="ubvec">
        ///     (NULL) This is an array of size ncon that specifies the allowed load imbalance tolerance for each constraint.
        ///     For the ith partition and jth constraint the allowed weight is the ubvec[j]*tpwgts[i*ncon+j] fraction
        ///     of the jth’s constraint total weight. The load imbalances must be greater than 1.0.
        ///     A NULL value can be passed indicating that the load imbalance tolerance for each constraint should
        ///     be 1.001 (for ncon=1) or 1.01 (for ncon¿1).
        /// </param>
        /// <param name="options">
        ///     This is the array of options as described in Section 5.4.
        ///     The following options are valid for METIS PartGraphRecursive:
        ///     METIS_OPTION_CTYPE, METIS_OPTION_IPTYPE, METIS_OPTION_RTYPE,
        ///     METIS_OPTION_NCUTS, METIS_OPTION_NITER, METIS_OPTION_SEED,
        ///     METIS_OPTION_UFACTOR, METIS_OPTION_NUMBERING, METIS_OPTION_DBGLVL
        ///     The following options are valid for METIS PartGraphKway:
        ///     METIS_OPTION_OBJTYPE, METIS_OPTION_CTYPE, METIS_OPTION_IPTYPE,
        ///     METIS_OPTION_RTYPE, METIS_OPTION_NCUTS, METIS_OPTION_NITER,
        ///     METIS_OPTION_UFACTOR, METIS_OPTION_MINCONN, METIS_OPTION_CONTIG,
        ///     METIS_OPTION_SEED, METIS_OPTION_NUMBERING, METIS_OPTION_DBGLVL
        /// </param>
        /// <param name="edgecut">
        ///     Upon successful completion, this variable stores the number of edges that are cut by the partition.
        /// </param>
        /// <param name="part">
        ///     This is a vector of size nvtxs that upon successful completion stores the partition vector of the graph.
        ///     The numbering of this vector starts from either 0 or 1, depending on the value of
        ///     options[METIS OPTION NUMBERING]
        /// </param>
        /// <returns>
        ///     METIS OK Indicates that the function returned normally.
        ///     METIS ERROR INPUT Indicates an input error.
        ///     METIS ERROR MEMORY Indicates that it could not allocate the required memory.
        ///     METIS ERROR Indicates some other type of error.
        /// </returns>
        [DllImport(DLL, CharSet = CharSet, CallingConvention = CallingConvention)]
        public static extern METISStatus METIS_PartGraphKway(int* nvtxs, int* ncon, int* xadj,
            int* adjncy, int* vwgt, int* vsize, int* adjwgt,
            int* nparts, float* tpwgts, float* ubvec, METISOptions* options,
            int* edgecut, int* part);
    }

    [SuppressMessage("ReSharper", "InconsistentNaming")]
    public enum METISStatus
    {
        METIS_OK = 1, /*!< Returned normally */
        METIS_ERROR_INPUT = -2, /*!< Returned due to erroneous inputs and/or options */
        METIS_ERROR_MEMORY = -3, /*!< Returned due to insufficient memory */
        METIS_ERROR = -4, /*!< Some other errors */
    }

    [SuppressMessage("ReSharper", "InconsistentNaming")]
    public enum METISOptions
    {
        METIS_OPTION_PTYPE,
        METIS_OPTION_OBJTYPE,
        METIS_OPTION_CTYPE,
        METIS_OPTION_IPTYPE,
        METIS_OPTION_RTYPE,
        METIS_OPTION_DBGLVL,
        METIS_OPTION_NIPARTS,
        METIS_OPTION_NITER,
        METIS_OPTION_NCUTS,
        METIS_OPTION_SEED,
        METIS_OPTION_ONDISK,
        METIS_OPTION_MINCONN,
        METIS_OPTION_CONTIG,
        METIS_OPTION_COMPRESS,
        METIS_OPTION_CCORDER,
        METIS_OPTION_PFACTOR,
        METIS_OPTION_NSEPS,
        METIS_OPTION_UFACTOR,
        METIS_OPTION_NUMBERING,
        METIS_OPTION_DROPEDGES,
        METIS_OPTION_NO2HOP,
        METIS_OPTION_TWOHOP,
        METIS_OPTION_FAST,

        /* Used for command-line parameter purposes */
        METIS_OPTION_HELP,
        METIS_OPTION_TPWGTS,
        METIS_OPTION_NCOMMON,
        METIS_OPTION_NOOUTPUT,
        METIS_OPTION_BALANCE,
        METIS_OPTION_GTYPE,
        METIS_OPTION_UBVEC,
    }
}