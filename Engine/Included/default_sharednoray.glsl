#template CustomFragmentData
#ifdef CustomFragmentData
#ifdef VERTEX
layout(location = 6) out FragmentData FragmentOut;
#else
layout(location = 6) in FragmentData FragmentIn;
#endif
#endif
